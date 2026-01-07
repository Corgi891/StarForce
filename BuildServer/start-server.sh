#!/bin/bash

echo "========================================"
echo "  StarForce 游戏服务器"
echo "========================================"
echo ""

# 检查.NET是否安装
if ! command -v dotnet &> /dev/null; then
    echo "错误: 未找到 .NET SDK"
    echo "请先安装 .NET 8.0 或更高版本"
    echo "下载地址: https://dotnet.microsoft.com/download"
    exit 1
fi

# 检查项目文件是否存在
if [ ! -f "GameServer.csproj" ]; then
    echo "错误: 未找到 GameServer.csproj 文件"
    exit 1
fi

# 还原NuGet包
echo "正在还原NuGet包..."
dotnet restore
if [ $? -ne 0 ]; then
    echo "错误: NuGet包还原失败"
    exit 1
fi

# 编译项目
echo "正在编译项目..."
dotnet build --configuration Release --no-incremental
if [ $? -ne 0 ]; then
    echo "错误: 项目编译失败"
    exit 1
fi

echo "编译成功！"
echo ""

# 运行服务器
echo "正在启动服务器..."
echo ""
dotnet run --configuration Release --no-build

