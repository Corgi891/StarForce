using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using StarForce.Server;

namespace StarForce.Server
{
    class Program
    {
        private static GameServer? server;
        private static bool isRunning = true;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("========================================");
            Console.WriteLine("  StarForce 游戏服务器");
            Console.WriteLine("========================================");
            Console.WriteLine();

            // 默认端口8000，可以通过命令行参数指定
            int port = 8000;
            if (args.Length > 0 && int.TryParse(args[0], out int customPort))
            {
                port = customPort;
            }

            try
            {
                server = new GameServer(IPAddress.Any, port);
                server.Start();

                Console.WriteLine($"服务器已启动，监听端口: {port}");
                Console.WriteLine($"客户端连接地址: 127.0.0.1:{port}");
                Console.WriteLine();
                Console.WriteLine("按 Ctrl+C 停止服务器");
                Console.WriteLine("----------------------------------------");

                // 等待Ctrl+C信号
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    isRunning = false;
                };

                // 主循环
                while (isRunning)
                {
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"服务器启动失败: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                server?.Stop();
                Console.WriteLine();
                Console.WriteLine("服务器已停止");
            }
        }
    }
}

