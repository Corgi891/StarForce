using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ProtoBuf;
using System.IO;

namespace StarForce.Server
{
    /// <summary>
    /// 游戏服务器主类
    /// </summary>
    public class GameServer
    {
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private TcpListener? _listener;
        private readonly List<ClientConnection> _clients = new List<ClientConnection>();
        private bool _isRunning = false;
        private readonly object _clientsLock = new object();

        public GameServer(IPAddress ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                return;
            }

            _listener = new TcpListener(_ipAddress, _port);
            _listener.Start();
            _isRunning = true;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 服务器开始监听 {_ipAddress}:{_port}");

            // 异步接受客户端连接
            Task.Run(AcceptClientsAsync);
        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;

            // 关闭所有客户端连接
            lock (_clientsLock)
            {
                foreach (var client in _clients.ToArray())
                {
                    client.Disconnect();
                }
                _clients.Clear();
            }

            _listener?.Stop();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 服务器已停止");
        }

        /// <summary>
        /// 异步接受客户端连接
        /// </summary>
        private async Task AcceptClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    var tcpClient = await _listener!.AcceptTcpClientAsync();
                    var client = new ClientConnection(tcpClient, this);
                    
                    lock (_clientsLock)
                    {
                        _clients.Add(client);
                    }

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 客户端已连接: {tcpClient.Client.RemoteEndPoint} (当前连接数: {_clients.Count})");

                    // 启动客户端处理任务
                    _ = Task.Run(() => client.HandleClientAsync());
                }
                catch (ObjectDisposedException)
                {
                    // 服务器已停止，正常退出
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 接受客户端连接时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 移除客户端连接
        /// </summary>
        public void RemoveClient(ClientConnection client)
        {
            lock (_clientsLock)
            {
                _clients.Remove(client);
            }
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 客户端已断开: {client.RemoteEndPoint} (当前连接数: {_clients.Count})");
        }
    }
}

