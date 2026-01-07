@echo off
chcp 65001 >nul
echo ========================================
echo   关闭 StarForce 服务器
echo ========================================
echo.

set PORT=8080

:: 查找并关闭占用8080端口的进程
echo 正在查找占用 %PORT% 端口的进程...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :%PORT% ^| findstr LISTENING') do (
    echo 找到进程 ID: %%a
    taskkill /F /PID %%a >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo 已关闭进程 %%a
    ) else (
        echo 无法关闭进程 %%a（可能需要管理员权限）
    )
)

:: 关闭所有Python进程（可选，谨慎使用）
echo.
echo 是否关闭所有Python进程？(Y/N)
set /p CLOSE_PYTHON=
if /i "%CLOSE_PYTHON%"=="Y" (
    echo 正在关闭Python进程...
    taskkill /F /IM python.exe >nul 2>&1
    taskkill /F /IM python3.exe >nul 2>&1
    echo 已关闭Python进程
)

echo.
echo 检查 %PORT% 端口状态...
netstat -ano | findstr :%PORT% >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo 警告: %PORT% 端口仍被占用
    netstat -ano | findstr :%PORT%
) else (
    echo %PORT% 端口已释放
)

echo.
echo 完成！
pause

