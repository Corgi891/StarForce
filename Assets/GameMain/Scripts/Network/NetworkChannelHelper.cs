//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using GameFramework;
using GameFramework.Event;
using GameFramework.Network;
using ProtoBuf;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityGameFramework.Runtime;

namespace StarForce
{
    public class NetworkChannelHelper : INetworkChannelHelper
    {
        private readonly Dictionary<int, Type> m_ServerToClientPacketTypes = new Dictionary<int, Type>();
        private readonly MemoryStream m_CachedStream = new MemoryStream(1024 * 8);
        private INetworkChannel m_NetworkChannel = null;

        /// <summary>
        /// 获取消息包头长度。
        /// </summary>
        public int PacketHeaderLength
        {
            get
            {
                return sizeof(int);
            }
        }

        /// <summary>
        /// 初始化网络频道辅助器。
        /// </summary>
        /// <param name="networkChannel">网络频道。</param>
        public void Initialize(INetworkChannel networkChannel)
        {
            m_NetworkChannel = networkChannel;

            // 反射注册包和包处理函数。
            Type packetBaseType = typeof(SCPacketBase);
            Type packetHandlerBaseType = typeof(PacketHandlerBase);
            Assembly assembly = Assembly.GetExecutingAssembly();
            Type[] types = assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                if (!types[i].IsClass || types[i].IsAbstract)
                {
                    continue;
                }

                if (types[i].BaseType == packetBaseType)
                {
                    PacketBase packetBase = (PacketBase)Activator.CreateInstance(types[i]);
                    Type packetType = GetServerToClientPacketType(packetBase.Id);
                    if (packetType != null)
                    {
                        Log.Warning("Already exist packet type '{0}', check '{1}' or '{2}'?.", packetBase.Id.ToString(), packetType.Name, packetBase.GetType().Name);
                        continue;
                    }

                    m_ServerToClientPacketTypes.Add(packetBase.Id, types[i]);
                }
                else if (types[i].BaseType == packetHandlerBaseType)
                {
                    IPacketHandler packetHandler = (IPacketHandler)Activator.CreateInstance(types[i]);
                    m_NetworkChannel.RegisterHandler(packetHandler);
                }
            }

            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkConnectedEventArgs.EventId, OnNetworkConnected);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkClosedEventArgs.EventId, OnNetworkClosed);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs.EventId, OnNetworkMissHeartBeat);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkErrorEventArgs.EventId, OnNetworkError);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkCustomErrorEventArgs.EventId, OnNetworkCustomError);
        }

        /// <summary>
        /// 关闭并清理网络频道辅助器。
        /// </summary>
        public void Shutdown()
        {
            GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkConnectedEventArgs.EventId, OnNetworkConnected);
            GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkClosedEventArgs.EventId, OnNetworkClosed);
            GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs.EventId, OnNetworkMissHeartBeat);
            GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkErrorEventArgs.EventId, OnNetworkError);
            GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkCustomErrorEventArgs.EventId, OnNetworkCustomError);

            m_NetworkChannel = null;
        }

        /// <summary>
        /// 准备进行连接。
        /// </summary>
        public void PrepareForConnecting()
        {
            m_NetworkChannel.Socket.ReceiveBufferSize = 1024 * 64;
            m_NetworkChannel.Socket.SendBufferSize = 1024 * 64;
        }

        /// <summary>
        /// 发送心跳消息包。
        /// </summary>
        /// <returns>是否发送心跳消息包成功。</returns>
        public bool SendHeartBeat()
        {
            m_NetworkChannel.Send(ReferencePool.Acquire<CSHeartBeat>());
            return true;
        }

        /// <summary>
        /// 序列化消息包。
        /// </summary>
        /// <typeparam name="T">消息包类型。</typeparam>
        /// <param name="packet">要序列化的消息包。</param>
        /// <param name="destination">要序列化的目标流。</param>
        /// <returns>是否序列化成功。</returns>
        public bool Serialize<T>(T packet, Stream destination) where T : Packet
        {
            PacketBase packetImpl = packet as PacketBase;
            if (packetImpl == null)
            {
                Log.Warning("Packet is invalid.");
                return false;
            }

            if (packetImpl.PacketType != PacketType.ClientToServer)
            {
                Log.Warning("Send packet invalid.");
                return false;
            }

            m_CachedStream.Position = 0L;
            m_CachedStream.SetLength(0);

            // 预留包头空间（使用PacketHeaderLength）
            int headerReservedSize = PacketHeaderLength;
            m_CachedStream.SetLength(headerReservedSize);
            m_CachedStream.Position = headerReservedSize;

            // 先序列化消息包体（带Fixed32长度前缀）
            RuntimeTypeModel.Default.SerializeWithLengthPrefix(m_CachedStream, packet, packet.GetType(), PrefixStyle.Fixed32, 0);
            int packetBodyLength = (int)(m_CachedStream.Length - headerReservedSize); // 包体长度（不包括预留的包头空间）

            // 序列化消息包头，设置Id和PacketLength
            CSPacketHeader packetHeader = ReferencePool.Acquire<CSPacketHeader>();
            packetHeader.Id = packetImpl.Id;
            packetHeader.PacketLength = packetBodyLength;
            
            // 序列化包头到预留空间
            m_CachedStream.Position = 0L;
            Serializer.Serialize(m_CachedStream, packetHeader);
            int actualHeaderLength = (int)m_CachedStream.Position;
            
            // 如果包头实际长度超过预留空间，需要移动包体数据
            if (actualHeaderLength > headerReservedSize)
            {
                // 读取包体数据
                byte[] bodyData = new byte[packetBodyLength];
                m_CachedStream.Position = headerReservedSize;
                m_CachedStream.Read(bodyData, 0, packetBodyLength);
                
                // 重新组织流：包头 + 包体（紧凑排列）
                m_CachedStream.Position = 0L;
                m_CachedStream.SetLength(actualHeaderLength + packetBodyLength);
                m_CachedStream.Write(bodyData, 0, packetBodyLength);
            }
            // 如果包头长度等于预留空间，流已经正确，无需调整
            
            ReferencePool.Release(packetHeader);

            // 将整个流（消息包头 + 消息包体）写入目标流
            m_CachedStream.Position = 0L;
            
            // 记录发送日志（打印包体结构数据）
            string packetBodyInfo = FormatPacketBody(packet);
            Log.Info("<color=white>[发送消息] Id={0}, 包体={1}</color>", packetImpl.Id, packetBodyInfo);
            
            m_CachedStream.WriteTo(destination);
            ReferencePool.Release((IReference)packet);
            
            return true;
        }

        /// <summary>
        /// 反序列化消息包头。
        /// </summary>
        /// <param name="source">要反序列化的来源流。</param>
        /// <param name="customErrorData">用户自定义错误数据。</param>
        /// <returns>反序列化后的消息包头。</returns>
        public IPacketHeader DeserializePacketHeader(Stream source, out object customErrorData)
        {
            // 注意：此函数并不在主线程调用！
            customErrorData = null;
            return (IPacketHeader)RuntimeTypeModel.Default.Deserialize(source, ReferencePool.Acquire<SCPacketHeader>(), typeof(SCPacketHeader));
        }

        /// <summary>
        /// 反序列化消息包。
        /// </summary>
        /// <param name="packetHeader">消息包头。</param>
        /// <param name="source">要反序列化的来源流。</param>
        /// <param name="customErrorData">用户自定义错误数据。</param>
        /// <returns>反序列化后的消息包。</returns>
        public Packet DeserializePacket(IPacketHeader packetHeader, Stream source, out object customErrorData)
        {
            // 注意：此函数并不在主线程调用！
            customErrorData = null;

            SCPacketHeader scPacketHeader = packetHeader as SCPacketHeader;
            if (scPacketHeader == null)
            {
                Log.Warning("Packet header is invalid.");
                return null;
            }

            Packet packet = null;
            if (scPacketHeader.IsValid)
            {
                Type packetType = GetServerToClientPacketType(scPacketHeader.Id);
                if (packetType != null)
                {
                    packet = (Packet)RuntimeTypeModel.Default.DeserializeWithLengthPrefix(source, ReferencePool.Acquire(packetType), packetType, PrefixStyle.Fixed32, 0);
                    
                    if (packet != null)
                    {
                        string packetBodyInfo = FormatPacketBody(packet);
                        Log.Info("<color=yellow>[接收消息] Id={0}, 包体={1}</color>", scPacketHeader.Id, packetBodyInfo);
                    }
                }
                else
                {
                    Log.Warning("Can not deserialize packet for packet id '{0}'.", scPacketHeader.Id.ToString());
                }
            }
            else
            {
                Log.Warning("Packet header is invalid.");
            }

            ReferencePool.Release(scPacketHeader);
            return packet;
        }

        /// <summary>
        /// 格式化包体数据为JSON字符串（用于日志输出）。
        /// </summary>
        private string FormatPacketBody(Packet packet)
        {
            if (packet == null)
            {
                return "null";
            }

            try
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                Type packetType = packet.GetType();
                System.Reflection.PropertyInfo[] properties = packetType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                
                sb.Append("{");
                bool first = true;
                foreach (var prop in properties)
                {
                    // 跳过 Id 属性（已经在包头中）
                    if (prop.Name == "Id" || prop.GetCustomAttributes(typeof(ProtoBuf.ProtoIgnoreAttribute), false).Length > 0)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        sb.Append(",");
                    }
                    first = false;

                    object value = prop.GetValue(packet);
                    sb.AppendFormat("\"{0}\":", prop.Name);
                    
                    if (value == null)
                    {
                        sb.Append("null");
                    }
                    else if (value is string)
                    {
                        // 转义JSON字符串中的特殊字符
                        string strValue = value.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                        sb.AppendFormat("\"{0}\"", strValue);
                    }
                    else if (value is bool)
                    {
                        sb.Append(value.ToString().ToLower());
                    }
                    else if (value is System.Collections.IEnumerable && !(value is string))
                    {
                        // 数组或集合
                        sb.Append("[");
                        bool arrayFirst = true;
                        foreach (var item in (System.Collections.IEnumerable)value)
                        {
                            if (!arrayFirst)
                            {
                                sb.Append(",");
                            }
                            arrayFirst = false;
                            sb.Append(FormatValue(item));
                        }
                        sb.Append("]");
                    }
                    else
                    {
                        sb.Append(FormatValue(value));
                    }
                }
                sb.Append("}");

                return sb.Length > 2 ? sb.ToString() : "{}";
            }
            catch (Exception ex)
            {
                return $"格式化失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 格式化单个值为JSON格式。
        /// </summary>
        private string FormatValue(object value)
        {
            if (value == null)
            {
                return "null";
            }
            
            Type valueType = value.GetType();
            
            // 基本类型
            if (valueType.IsPrimitive || valueType == typeof(decimal))
            {
                return value.ToString();
            }
            
            // 字符串
            if (valueType == typeof(string))
            {
                string strValue = value.ToString().Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                return $"\"{strValue}\"";
            }
            
            // 布尔值
            if (valueType == typeof(bool))
            {
                return value.ToString().ToLower();
            }
            
            // 其他对象，尝试转换为字符串
            return $"\"{value}\"";
        }

        private Type GetServerToClientPacketType(int id)
        {
            Type type = null;
            if (m_ServerToClientPacketTypes.TryGetValue(id, out type))
            {
                return type;
            }

            return null;
        }

        private void OnNetworkConnected(object sender, GameEventArgs e)
        {
            UnityGameFramework.Runtime.NetworkConnectedEventArgs ne = (UnityGameFramework.Runtime.NetworkConnectedEventArgs)e;
            if (ne.NetworkChannel != m_NetworkChannel)
            {
                return;
            }

            Log.Info("Network channel '{0}' connected, local address '{1}', remote address '{2}'.", ne.NetworkChannel.Name, ne.NetworkChannel.Socket.LocalEndPoint.ToString(), ne.NetworkChannel.Socket.RemoteEndPoint.ToString());
        }

        private void OnNetworkClosed(object sender, GameEventArgs e)
        {
            UnityGameFramework.Runtime.NetworkClosedEventArgs ne = (UnityGameFramework.Runtime.NetworkClosedEventArgs)e;
            if (ne.NetworkChannel != m_NetworkChannel)
            {
                return;
            }

            Log.Info("Network channel '{0}' closed.", ne.NetworkChannel.Name);
        }

        private void OnNetworkMissHeartBeat(object sender, GameEventArgs e)
        {
            UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs ne = (UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs)e;
            if (ne.NetworkChannel != m_NetworkChannel)
            {
                return;
            }

            Log.Info("Network channel '{0}' miss heart beat '{1}' times.", ne.NetworkChannel.Name, ne.MissCount.ToString());

            if (ne.MissCount < 2)
            {
                return;
            }

            ne.NetworkChannel.Close();
        }

        private void OnNetworkError(object sender, GameEventArgs e)
        {
            UnityGameFramework.Runtime.NetworkErrorEventArgs ne = (UnityGameFramework.Runtime.NetworkErrorEventArgs)e;
            if (ne.NetworkChannel != m_NetworkChannel)
            {
                return;
            }

            Log.Info("Network channel '{0}' error, error code is '{1}', error message is '{2}'.", ne.NetworkChannel.Name, ne.ErrorCode.ToString(), ne.ErrorMessage);

            ne.NetworkChannel.Close();
        }

        private void OnNetworkCustomError(object sender, GameEventArgs e)
        {
            UnityGameFramework.Runtime.NetworkCustomErrorEventArgs ne = (UnityGameFramework.Runtime.NetworkCustomErrorEventArgs)e;
            if (ne.NetworkChannel != m_NetworkChannel)
            {
                return;
            }
        }
    }
}
