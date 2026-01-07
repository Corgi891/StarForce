using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;
using ProtoBuf.Meta;
using StarForce;

namespace StarForce.Server
{
    /// <summary>
    /// 客户端连接处理类
    /// </summary>
    public class ClientConnection
    {
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly GameServer _server;
        private bool _isConnected = true;
        private readonly byte[] _headerBuffer = new byte[4]; // 消息包头长度
        private byte? _savedFirstByte = null; // 保存的第一个字节（当消息包头为空时）

        public string RemoteEndPoint
        {
            get
            {
                try
                {
                    return _tcpClient?.Client?.RemoteEndPoint?.ToString() ?? "Unknown";
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

        public ClientConnection(TcpClient tcpClient, GameServer server)
        {
            _tcpClient = tcpClient;
            _stream = tcpClient.GetStream();
            _server = server;

            // 设置Socket缓冲区大小
            _tcpClient.ReceiveBufferSize = 1024 * 64;
            _tcpClient.SendBufferSize = 1024 * 64;
        }

        /// <summary>
        /// 处理客户端连接
        /// </summary>
        public async Task HandleClientAsync()
        {
            try
            {
                while (_isConnected && _tcpClient.Connected)
                {
                    // 读取消息包头（使用流式读取，因为ProtoBuf是变长的）
                    CSPacketHeader? packetHeader = await DeserializePacketHeaderAsync();
                    if (packetHeader == null)
                    {
                        break; // 连接已关闭或出错
                    }

                    // 读取消息包体
                    // PacketLength 已经告诉我们包体的总长度（包括Fixed32长度前缀 + 实际包体数据）
                    // 直接读取 PacketLength 字节的数据即可
                    int packetBodyLength = packetHeader.PacketLength;
                    
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] 消息包头: Id={packetHeader.Id}, PacketLength={packetBodyLength}");
                    
                    byte[] packetBodyBuffer = null;
                    if (packetBodyLength > 0)
                    {
                        // 读取包体数据（包括Fixed32长度前缀）
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] 开始读取包体数据（{packetBodyLength}字节）");
                        packetBodyBuffer = new byte[packetBodyLength];
                        
                        // 如果消息包头为空，第一个字节已经保存在 _savedFirstByte 中
                        if (_savedFirstByte.HasValue)
                        {
                            packetBodyBuffer[0] = _savedFirstByte.Value;
                            int remainingBytes = await ReadBytesAsync(packetBodyBuffer, 1, packetBodyLength - 1);
                            if (remainingBytes != packetBodyLength - 1)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 读取消息包体失败（读取了{remainingBytes + 1}字节，需要{packetBodyLength}字节）");
                                break;
                            }
                            _savedFirstByte = null; // 清除保存的字节
                        }
                        else
                        {
                            int bodyBytesRead = await ReadBytesAsync(packetBodyBuffer, 0, packetBodyLength);
                            if (bodyBytesRead != packetBodyLength)
                            {
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 读取消息包体失败（读取了{bodyBytesRead}字节，需要{packetBodyLength}字节）");
                                break;
                            }
                        }
                        
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] 成功读取消息包体（{packetBodyLength}字节）: {BitConverter.ToString(packetBodyBuffer)}");
                        
                        // 对于心跳包（PacketLength=4），包体只有Fixed32长度前缀，没有实际数据
                        // 可以解析长度前缀验证（可选）
                        if (packetBodyLength >= 4)
                        {
                            int actualBodyLength = BitConverter.ToInt32(packetBodyBuffer, 0);
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] 包体长度前缀解析值: {actualBodyLength} 字节（实际包体数据长度）");
                        }
                    }
                    else
                    {
                        // 消息包体长度为0（理论上不应该发生，因为至少会有Fixed32长度前缀）
                        packetBodyBuffer = new byte[0];
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 警告：消息包体长度为0");
                    }

                    // 处理消息包
                    // 注意：根据客户端代码，消息包头的Id应该已经设置了
                    // 但如果消息包头是空的（Id=0），我们从消息包体中推断
                    int packetId = packetHeader?.Id ?? 0;
                    if (packetId == 0)
                    {
                        // 消息包头没有Id，尝试从消息包体中推断（默认假设是心跳包）
                        packetId = 1;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 消息包头Id为0，假设是心跳包（ID=1）");
                    }
                    else
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 消息包头Id: {packetId}");
                    }
                    
                    await ProcessPacketAsync(packetId, packetBodyBuffer);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 处理客户端 {RemoteEndPoint} 时出错: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  内部异常: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
                Console.WriteLine($"  堆栈跟踪: {ex.StackTrace}");
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>
        /// 读取指定长度的字节
        /// </summary>
        private async Task<int> ReadBytesAsync(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count && _isConnected)
            {
                int bytesRead = await _stream.ReadAsync(buffer, offset + totalRead, count - totalRead);
                if (bytesRead == 0)
                {
                    break; // 连接已关闭
                }
                totalRead += bytesRead;
            }
            return totalRead;
        }

        /// <summary>
        /// 异步反序列化消息包头（使用流式读取）
        /// </summary>
        private async Task<CSPacketHeader?> DeserializePacketHeaderAsync()
        {
            try
            {
                // 使用流式反序列化，ProtoBuf会自动处理变长编码
                // 创建一个包装流来异步读取
                using (var headerStream = new MemoryStream())
                {
                    // 先尝试读取0字节（处理空消息包头的情况）
                    // 如果消息包头是空的（所有字段都是默认值），ProtoBuf可能序列化为0字节
                    byte[] buffer = new byte[256];
                    int totalRead = 0;
                    
                    // 先尝试读取至少1字节，看看是否有数据
                    int bytesRead = await _stream.ReadAsync(buffer, 0, 1);
                    if (bytesRead == 0)
                    {
                        return null; // 连接已关闭，没有数据
                    }
                    totalRead = bytesRead;
                    
                    // 打印读取的第一个字节
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] 读取包头第一个字节: 0x{buffer[0]:X2}");
                    
                    // 检查第一个字节是否是消息包体的开始（Fixed32长度前缀的第一个字节）
                    // 如果消息包头是空的（0字节），第一个字节应该是Fixed32长度前缀的第一个字节
                    // 有效的ProtoBuf字段标记的第一个字节应该是 0x08（字段1，WireType=0）或 0x10（字段2，WireType=0）
                    // 如果第一个字节是 0x00，很可能是包体长度前缀的开始（消息包头为空）
                    byte firstByte = buffer[0];
                    
                    // 如果第一个字节是 0x00，很可能是消息包头为空，第一个字节是包体长度前缀的开始
                    // ProtoBuf字段标记不会是 0x00（字段号范围是1-536870911，WireType范围是0-7）
                    // 所以如果第一个字节是 0x00，肯定是包体长度前缀的开始
                    if (firstByte == 0x00)
                    {
                        // 消息包头为空，保存第一个字节，返回空消息包头
                        // 注意：我们需要将第一个字节保存，以便在读取包体长度前缀时使用
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] 检测到消息包头为空（0字节），第一个字节是0x00（包体长度前缀的开始），返回空消息包头");
                        _savedFirstByte = firstByte;
                        // 返回空消息包头（所有字段都是默认值）
                        return new CSPacketHeader { Id = 0, PacketLength = 0 };
                    }
                    
                    // 如果第一个字节不是0x00，继续正常解析消息包头
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] 第一个字节不是0x00（实际值: 0x{firstByte:X2}），继续正常解析消息包头");
                    
                    // 逐字节读取，直到能够成功反序列化
                    while (totalRead < buffer.Length && _isConnected && _tcpClient.Connected)
                    {
                        // 每次读取后尝试反序列化
                        try
                        {
                            headerStream.SetLength(0);
                            headerStream.Write(buffer, 0, totalRead);
                            headerStream.Position = 0;
                            
                            // 记录反序列化前的位置
                            long beforeDeserialize = headerStream.Position;
                            
                            // 检查第一个字节是否是有效的ProtoBuf字段标记
                            // 有效的包头第一个字节应该是 0x08（字段1，WireType=0）或 0x10（字段2，WireType=0）
                            if (totalRead >= 1 && buffer[0] != 0x08 && buffer[0] != 0x10)
                            {
                                // 第一个字节不是有效的ProtoBuf字段标记，可能是包体数据或连接已关闭
                                // 输出调试信息
                                string hexData = BitConverter.ToString(buffer, 0, totalRead);
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 警告：读取的数据第一个字节不是有效的ProtoBuf字段标记（0x{buffer[0]:X2}），可能是包体数据或连接已关闭");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 读取的数据（十六进制）: {hexData}");
                                
                                // 如果第一个字节是0x00，说明消息包头为空，第一个字节是包体长度前缀的开始
                                if (buffer[0] == 0x00)
                                {
                                    // 检查是否所有字节都是0x00（可能是包体长度前缀 00-00-00-00）
                                    bool allZeros = true;
                                    for (int i = 0; i < totalRead; i++)
                                    {
                                        if (buffer[i] != 0x00)
                                        {
                                            allZeros = false;
                                            break;
                                        }
                                    }
                                    
                                    if (allZeros && totalRead >= 4)
                                    {
                                        // 读取到4字节全0，说明消息包头为空，这是包体长度前缀
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 检测到消息包头为空（读取到4字节全0，包体长度前缀），返回空消息包头");
                                        _savedFirstByte = buffer[0];
                                        return new CSPacketHeader { Id = 0, PacketLength = 0 };
                                    }
                                    else if (allZeros)
                                    {
                                        // 读取到全0但不足4字节，可能是连接已关闭
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 检测到连接可能已关闭（读取到全0数据但不足4字节），返回null");
                                        return null;
                                    }
                                }
                                
                                // 如果不是全0，可能是数据格式错误，继续读取看看
                                // 但如果已经读取了足够多字节（比如4字节）仍然无效，可能是真正的错误
                                if (totalRead >= 4)
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 已读取{totalRead}字节但第一个字节仍然无效，可能是数据格式错误或连接已关闭");
                                    return null;
                                }
                            }
                            
                            // 使用 RuntimeTypeModel 反序列化，更可靠
                            CSPacketHeader header = null;
                            try
                            {
                                header = RuntimeTypeModel.Default.Deserialize(headerStream, null, typeof(CSPacketHeader)) as CSPacketHeader;
                            }
                            catch
                            {
                                // 如果 RuntimeTypeModel 失败，尝试使用 Serializer
                                headerStream.Position = beforeDeserialize;
                                header = Serializer.Deserialize<CSPacketHeader>(headerStream);
                            }
                            
                            long afterDeserialize = headerStream.Position;
                            long bytesConsumed = afterDeserialize - beforeDeserialize;
                            
                            if (header != null)
                            {
                                // 检查是否读取了所有数据
                                bool allBytesConsumed = bytesConsumed == totalRead;
                                
                                // 验证关键字段：如果 Id>0 但 PacketLength==0，说明数据不完整
                                bool isDataComplete = !(header.Id > 0 && header.PacketLength == 0 && totalRead > 0);
                                
                                if (allBytesConsumed && isDataComplete)
                                {
                                    // 成功反序列化，并且消耗了所有读取的字节，且数据完整
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 成功反序列化消息包头（读取了{totalRead}字节，消耗了{bytesConsumed}字节）");
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 消息包头: Id={header.Id}, PacketLength={header.PacketLength}");
                                    return header;
                                }
                                else
                                {
                                    // 数据不完整，继续读取
                                    if (!allBytesConsumed)
                                    {
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 部分反序列化（读取了{totalRead}字节，只消耗了{bytesConsumed}字节），继续读取...");
                                    }
                                    else if (!isDataComplete)
                                    {
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 警告：Id={header.Id} 但 PacketLength=0，数据不完整，继续读取...");
                                    }
                                    // 继续循环读取更多字节
                                }
                            }
                        }
                        catch (InvalidOperationException ex)
                        {
                            // ProtoBuf数据不完整，继续读取
                            if (ex.Message.Contains("end of stream") || ex.Message.Contains("No data"))
                            {
                                // 继续读取
                            }
                            else
                            {
                                // 其他InvalidOperationException，可能是真正的错误
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 反序列化消息包头时出错（已读取{totalRead}字节）: {ex.Message}");
                                return null;
                            }
                        }
                        catch (EndOfStreamException)
                        {
                            // 数据流结束，但可能数据不完整，继续读取
                        }
                        catch (ProtoBuf.ProtoException protoEx)
                        {
                            // ProtoBuf特定错误
                            // 如果数据不完整，ProtoBuf可能会抛出异常，这是正常的，继续读取
                            if (protoEx.Message.Contains("Invalid field") || protoEx.Message.Contains("field in source data"))
                            {
                                // 输出读取的数据用于调试
                                string hexData = BitConverter.ToString(buffer, 0, totalRead);
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ProtoBuf格式错误（已读取{totalRead}字节）: {protoEx.Message}");
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 读取的数据（十六进制）: {hexData}");
                                
                                // 检查读取的数据是否是有效的包头格式
                                // 有效的包头格式：第一个字节应该是 0x08（字段1，WireType=0）或 0x10（字段2，WireType=0）
                                if (totalRead >= 1 && buffer[0] != 0x08 && buffer[0] != 0x10)
                                {
                                    // 第一个字节不是有效的ProtoBuf字段标记
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 第一个字节不是有效的ProtoBuf字段标记（期望0x08或0x10，实际0x{buffer[0]:X2}）");
                                    
                                    // 如果第一个字节是0x00，说明消息包头为空，第一个字节是包体长度前缀的开始
                                    if (buffer[0] == 0x00)
                                    {
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 检测到第一个字节是0x00，消息包头为空，保存第一个字节并返回空消息包头");
                                        // 保存第一个字节，返回空消息包头
                                        _savedFirstByte = buffer[0];
                                        return new CSPacketHeader { Id = 0, PacketLength = 0 };
                                    }
                                    
                                    // 如果已经读取了足够多字节（比如4字节）仍然无效，可能是真正的错误
                                    if (totalRead >= 4)
                                    {
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 已读取{totalRead}字节但第一个字节仍然无效，可能是数据格式错误或连接已关闭");
                                        return null;
                                    }
                                    
                                    // 如果读取的字节数较少，可能是数据不完整，继续读取
                                }
                                else if (totalRead >= 1 && (buffer[0] == 0x08 || buffer[0] == 0x10))
                                {
                                    // 第一个字节是有效的字段标记，但反序列化失败
                                    // 可能是数据不完整，继续读取更多字节
                                    if (totalRead < 16) // 最多读取16字节，包头通常不会超过这个长度
                                    {
                                        // 数据可能不完整，继续读取
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 第一个字节有效但反序列化失败，可能是数据不完整，继续读取...");
                                    }
                                    else
                                    {
                                        // 读取了足够多字节但仍然失败，可能是真正的格式错误
                                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 已读取{totalRead}字节但反序列化仍然失败，可能是真正的格式错误");
                                        return null;
                                    }
                                }
                                else
                                {
                                    // 其他情况，可能是数据不完整，继续读取
                                    if (totalRead < 16)
                                    {
                                        // 继续读取
                                    }
                                    else
                                    {
                                        // 读取了足够多字节但仍然失败
                                        return null;
                                    }
                                }
                            }
                            // 其他ProtoException，可能是数据不完整，继续读取
                        }
                        catch (Exception ex)
                        {
                            // 其他错误
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 反序列化消息包头时出错（已读取{totalRead}字节）: {ex.GetType().Name} - {ex.Message}");
                            return null;
                        }
                        
                        // 继续读取下一个字节
                        bytesRead = await _stream.ReadAsync(buffer, totalRead, 1);
                        if (bytesRead == 0)
                        {
                            break; // 连接关闭，但已有数据，尝试反序列化
                        }
                        totalRead += bytesRead;
                        
                        // 打印当前读取的所有字节
                        string currentHex = BitConverter.ToString(buffer, 0, totalRead);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [DEBUG] 读取包头进度: {totalRead}字节, 数据: {currentHex}");
                    }
                    
                    // 如果读取了数据但无法反序列化
                    if (totalRead > 0)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 无法反序列化消息包头（已读取{totalRead}字节）");
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 反序列化消息包头失败: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  内部异常: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
                return null;
            }
        }


        /// <summary>
        /// 处理消息包
        /// </summary>
        private async Task ProcessPacketAsync(int packetId, byte[] packetBody)
        {
            try
            {
                switch (packetId)
                {
                    case 1: // CSHeartBeat
                        await HandleHeartBeatAsync();
                        break;
                    case 100: // CSTestLogin
                        await HandleTestLoginAsync(packetBody);
                        break;
                    default:
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 收到未知消息包 ID: {packetId}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 处理消息包失败 (ID: {packetId}): {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  内部异常: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
                Console.WriteLine($"  堆栈跟踪: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 处理心跳包
        /// </summary>
        private async Task HandleHeartBeatAsync()
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 收到心跳包，准备发送响应");
            // 发送心跳响应
            await SendPacketAsync(new SCHeartBeat());
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 心跳响应已发送");
        }

        /// <summary>
        /// 处理测试登录包
        /// </summary>
        private async Task HandleTestLoginAsync(byte[] packetBody)
        {
            try
            {
                // 解析包体数据（跳过Fixed32长度前缀）
                if (packetBody == null || packetBody.Length < 4)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 登录包数据无效");
                    return;
                }

                // 解析长度前缀
                int actualBodyLength = BitConverter.ToInt32(packetBody, 0);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 登录包实际数据长度: {actualBodyLength} 字节");

                if (actualBodyLength > 0)
                {
                    // 反序列化登录包
                    using (MemoryStream bodyStream = new MemoryStream(packetBody, 4, actualBodyLength))
                    {
                        CSTestLogin loginPacket = Serializer.Deserialize<CSTestLogin>(bodyStream);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 收到登录请求: Username={loginPacket.Username}, Password={loginPacket.Password}");

                        // 模拟登录验证（简单测试）
                        bool success = !string.IsNullOrEmpty(loginPacket.Username) && loginPacket.Username == "test" && loginPacket.Password == "123456";
                        
                        // 发送登录响应
                        SCTestLogin response = new SCTestLogin
                        {
                            Success = success,
                            Message = success ? "登录成功" : "用户名或密码错误",
                            UserId = success ? 1001 : 0
                        };
                        
                        await SendPacketAsync(response);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 登录响应已发送: Success={response.Success}, Message={response.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 登录包数据为空");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 处理登录包失败: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  内部异常: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// 发送消息包
        /// </summary>
        private async Task SendPacketAsync(SCPacketBase packet)
        {
            if (!_isConnected || !_tcpClient.Connected)
            {
                return;
            }

            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    // 先序列化消息包体（带Fixed32长度前缀）到临时流，计算长度
                    using (MemoryStream bodyStream = new MemoryStream())
                    {
                        // 需要明确指定类型，因为 packet 是基类类型，ProtoBuf 无法推断继承类型
                        RuntimeTypeModel.Default.SerializeWithLengthPrefix(bodyStream, packet, packet.GetType(), PrefixStyle.Fixed32, 0);
                        int packetBodyLength = (int)bodyStream.Length; // 包括Fixed32长度前缀（4字节）+ 消息包体

                        // 序列化消息包头，设置Id和PacketLength
                        SCPacketHeader packetHeader = new SCPacketHeader();
                        packetHeader.Id = packet.Id;
                        packetHeader.PacketLength = packetBodyLength;
                        
                        // 先序列化消息包头
                        Serializer.Serialize(ms, packetHeader);
                        
                        // 再追加消息包体
                        bodyStream.Position = 0L;
                        bodyStream.CopyTo(ms);
                    }

                    // 发送数据
                    byte[] data = ms.ToArray();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 准备发送消息包，总长度: {data.Length} 字节");
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 消息包数据（十六进制）: {BitConverter.ToString(data)}");
                    await _stream.WriteAsync(data, 0, data.Length);
                    await _stream.FlushAsync();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 消息包已发送");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 发送消息包失败: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  内部异常: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }
                Console.WriteLine($"  堆栈跟踪: {ex.StackTrace}");
                Disconnect();
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            if (!_isConnected)
            {
                return;
            }

            _isConnected = false;

            try
            {
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch
            {
                // 忽略关闭时的异常
            }

            _server.RemoveClient(this);
        }
    }
}

