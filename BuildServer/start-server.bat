@echo off
chcp 65001 >nul
cd /d "%~dp0"

echo ========================================
echo   StarForce 游戏服务器
echo ========================================
echo.

:: 检查.NET是否安装
dotnet --version >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo 错误: 未找到 .NET SDK
    echo 请先安装 .NET 8.0 或更高版本
    echo 下载地址: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

:: 检查项目文件是否存在
if not exist "GameServer.csproj" (
    echo 错误: 未找到 GameServer.csproj 文件
    pause
    exit /b 1
)

:: 还原NuGet包
echo 正在还原NuGet包...
dotnet restore
if %ERRORLEVEL% NEQ 0 (
    echo 错误: NuGet包还原失败
    pause
    exit /b 1
)

:: 编译项目
echo 正在编译项目...
dotnet build --configuration Release --no-incremental
if %ERRORLEVEL% NEQ 0 (
    echo 错误: 项目编译失败
    pause
    exit /b 1
)

echo 编译成功！
echo.

:: 运行服务器
echo 正在启动服务器...
echo.
dotnet run --configuration Release --no-build

pause

