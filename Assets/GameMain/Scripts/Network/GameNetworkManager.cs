//------------------------------------------------------------
// Game Framework
// Copyright © 2013-2021 Jiang Yin. All rights reserved.
// Homepage: https://gameframework.cn/
// Feedback: mailto:ellan@gameframework.cn
//------------------------------------------------------------

using System.Net;
using GameFramework;
using GameFramework.Event;
using GameFramework.Network;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace StarForce
{
    /// <summary>
    /// 游戏网络管理器组件。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Star Force/Network Manager")]
    public class GameNetworkManager : GameFrameworkComponent
    {
        private const string NetworkChannelName = "GameChannel";
        private INetworkChannel m_GameChannel = null;
        private bool m_IsConnected = false;

        /// <summary>
        /// 获取是否已连接。
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return m_IsConnected && m_GameChannel != null && m_GameChannel.Connected;
            }
        }

        /// <summary>
        /// 获取网络频道。
        /// </summary>
        public INetworkChannel GameChannel
        {
            get
            {
                return m_GameChannel;
            }
        }

        /// <summary>
        /// 游戏框架组件初始化。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {
            // 订阅网络事件
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkConnectedEventArgs.EventId, OnNetworkConnected);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkClosedEventArgs.EventId, OnNetworkClosed);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs.EventId, OnNetworkMissHeartBeat);
            GameEntry.Event.Subscribe(UnityGameFramework.Runtime.NetworkErrorEventArgs.EventId, OnNetworkError);

            // 创建网络频道
            CreateNetworkChannel();
        }

        /// <summary>
        /// 创建网络频道。
        /// </summary>
        private void CreateNetworkChannel()
        {
            if (m_GameChannel != null)
            {
                return;
            }

            try
            {
                m_GameChannel = GameEntry.Network.CreateNetworkChannel(
                    NetworkChannelName,
                    ServiceType.Tcp,
                    new NetworkChannelHelper()
                );

                // 配置心跳间隔
                m_GameChannel.HeartBeatInterval = 5f;
                m_GameChannel.ResetHeartBeatElapseSecondsWhenReceivePacket = true;

                Log.Info("网络频道创建成功：{0}", NetworkChannelName);
            }
            catch (System.Exception ex)
            {
                Log.Error("创建网络频道失败：{0}", ex.Message);
            }
        }

        /// <summary>
        /// 连接到服务器。
        /// </summary>
        /// <param name="ip">服务器IP地址。</param>
        /// <param name="port">服务器端口。</param>
        public void Connect(string ip, int port)
        {
            if (m_GameChannel == null)
            {
                CreateNetworkChannel();
            }

            if (m_GameChannel == null)
            {
                Log.Error("网络频道未创建，无法连接服务器");
                return;
            }

            if (m_GameChannel.Connected)
            {
                Log.Warning("已经连接到服务器，无需重复连接");
                return;
            }

            try
            {
                IPAddress ipAddress = IPAddress.Parse(ip);
                m_GameChannel.Connect(ipAddress, port);
                Log.Info("正在连接到服务器：{0}:{1}", ip, port);
            }
            catch (System.Exception ex)
            {
                Log.Error("连接服务器失败：{0}", ex.Message);
            }
        }

        /// <summary>
        /// 连接到本地服务器（默认配置）。
        /// </summary>
        public void ConnectToLocalServer()
        {
            // 默认连接到本地服务器
            Connect("127.0.0.1", 8000);
        }

        /// <summary>
        /// 断开连接。
        /// </summary>
        public void Disconnect()
        {
            if (m_GameChannel != null && m_GameChannel.Connected)
            {
                m_GameChannel.Close();
                Log.Info("已断开服务器连接");
            }
        }

        private void OnDestroy()
        {
            // 取消订阅事件
            if (GameEntry.Event != null)
            {
                GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkConnectedEventArgs.EventId, OnNetworkConnected);
                GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkClosedEventArgs.EventId, OnNetworkClosed);
                GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs.EventId, OnNetworkMissHeartBeat);
                GameEntry.Event.Unsubscribe(UnityGameFramework.Runtime.NetworkErrorEventArgs.EventId, OnNetworkError);
            }

            // 断开连接
            Disconnect();

            // 销毁网络频道
            if (m_GameChannel != null && GameEntry.Network != null)
            {
                GameEntry.Network.DestroyNetworkChannel(NetworkChannelName);
                m_GameChannel = null;
            }

            m_IsConnected = false;
        }

        /// <summary>
        /// 发送消息包。
        /// </summary>
        /// <typeparam name="T">消息包类型。</typeparam>
        /// <param name="packet">要发送的消息包。</param>
        public void Send<T>(T packet) where T : Packet
        {
            if (!IsConnected)
            {
                Log.Warning("未连接到服务器，无法发送消息包");
                return;
            }

            if (m_GameChannel != null)
            {
                m_GameChannel.Send(packet);
            }
        }

        /// <summary>
        /// 测试发送登录包。
        /// </summary>
        /// <param name="username">用户名。</param>
        /// <param name="password">密码。</param>
        public void TestSendLogin(string username = "test", string password = "123456")
        {
            if (!IsConnected)
            {
                Log.Warning("网络未连接，无法发送登录包");
                return;
            }

            CSTestLogin loginPacket = ReferencePool.Acquire<CSTestLogin>();
            loginPacket.Username = username;
            loginPacket.Password = password;
            
            Send(loginPacket);
            Log.Info("发送登录包: Username={0}, Password={1}", username, password);
        }

        #region 网络事件处理

        private void OnNetworkConnected(object sender, GameEventArgs e)
        {
            UnityGameFramework.Runtime.NetworkConnectedEventArgs ne = (UnityGameFramework.Runtime.NetworkConnectedEventArgs)e;
            if (ne.NetworkChannel != m_GameChannel)
            {
                return;
            }

            m_IsConnected = true;
            Log.Info("网络连接成功：{0}，本地地址：{1}，远程地址：{2}",
                ne.NetworkChannel.Name,
                ne.NetworkChannel.Socket.LocalEndPoint?.ToString(),
                ne.NetworkChannel.Socket.RemoteEndPoint?.ToString());
        }

        private void OnNetworkClosed(object sender, GameEventArgs e)
        {
            UnityGameFramework.Runtime.NetworkClosedEventArgs ne = (UnityGameFramework.Runtime.NetworkClosedEventArgs)e;
            if (ne.NetworkChannel != m_GameChannel)
            {
                return;
            }

            m_IsConnected = false;
            Log.Info("网络连接关闭：{0}", ne.NetworkChannel.Name);
        }

        private void OnNetworkMissHeartBeat(object sender, GameEventArgs e)
        {
            UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs ne = (UnityGameFramework.Runtime.NetworkMissHeartBeatEventArgs)e;
            if (ne.NetworkChannel != m_GameChannel)
            {
                return;
            }

            Log.Warning("网络频道 '{0}' 丢失心跳 {1} 次", ne.NetworkChannel.Name, ne.MissCount.ToString());

            if (ne.MissCount >= 2)
            {
                Log.Error("丢失心跳次数过多，连接可能已断开");
            }
        }

        private void OnNetworkError(object sender, GameEventArgs e)
        {
            UnityGameFramework.Runtime.NetworkErrorEventArgs ne = (UnityGameFramework.Runtime.NetworkErrorEventArgs)e;
            if (ne.NetworkChannel != m_GameChannel)
            {
                return;
            }

            m_IsConnected = false;
            Log.Error("网络错误：{0}，错误码：{1}，错误信息：{2}",
                ne.NetworkChannel.Name,
                ne.ErrorCode.ToString(),
                ne.ErrorMessage);
        }

        #endregion
    }
}

