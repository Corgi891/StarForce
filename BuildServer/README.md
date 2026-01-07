# StarForce 游戏服务器

这是一个简单的TCP游戏服务器，用于与Unity客户端通信。

## 功能特性

- TCP服务器，监听客户端连接
- 支持ProtoBuf消息序列化/反序列化
- 处理心跳包（CSHeartBeat/SCHeartBeat）
- 支持多客户端连接

## 系统要求

- .NET 8.0 或更高版本
- Windows / Linux / macOS

## 快速开始

### Windows

1. 双击运行 `start-server.bat`
2. 服务器将在端口 8000 上启动（默认）

### Linux / macOS

1. 给脚本添加执行权限：
   ```bash
   chmod +x start-server.sh
   ```

2. 运行脚本：
   ```bash
   ./start-server.sh
   ```

### 手动运行

1. 还原NuGet包：
   ```bash
   dotnet restore
   ```

2. 编译项目：
   ```bash
   dotnet build
   ```

3. 运行服务器：
   ```bash
   dotnet run
   ```

## 配置

### 修改端口

默认端口是 8000，可以通过命令行参数修改：

```bash
dotnet run -- 8888
```

或在 `Program.cs` 中修改默认端口。

## 消息包格式

服务器使用与客户端相同的消息包格式：

1. **消息包头**：ProtoBuf序列化的 `CSPacketHeader`（客户端）或 `SCPacketHeader`（服务器）
2. **消息包体**：ProtoBuf序列化的消息包，带Fixed32长度前缀

### 支持的消息包

- **CSHeartBeat** (Id=1)：客户端心跳包
- **SCHeartBeat** (Id=2)：服务器心跳响应

## 日志输出

服务器会输出以下信息：
- 服务器启动/停止
- 客户端连接/断开
- 消息包处理
- 错误信息

## 注意事项

- 服务器默认监听所有网络接口（0.0.0.0）
- 确保防火墙允许TCP端口访问
- 消息包大小限制为1MB

## 扩展开发

要添加新的消息包处理：

1. 在 `ClientConnection.cs` 的 `ProcessPacketAsync` 方法中添加新的case分支
2. 实现相应的处理逻辑

示例：
```csharp
case 100: // 新的消息包ID
    await HandleNewPacketAsync(packetBody);
    break;
```

