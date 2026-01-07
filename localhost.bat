@echo off
chcp 65001 >nul
cd /d "%~dp0"

set PORT=8080
set PATH_DIR=%~dp0Build

:: 检查Python是否可用（严格检测，确保真的能运行）
set USE_PYTHON=0
python -c "import sys, http.server; exit(0)" >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    set USE_PYTHON=1
    set PYTHON_CMD=python
) else (
    python3 -c "import sys, http.server; exit(0)" >nul 2>nul
    if %ERRORLEVEL% EQU 0 (
        set USE_PYTHON=1
        set PYTHON_CMD=python3
    )
)

if %USE_PYTHON% EQU 1 (
    echo ========================================
    echo   StarForce 本地热更服务器
    echo ========================================
    echo.
    echo 资源路径: %PATH_DIR%
    echo 访问地址: http://localhost:%PORT%
    echo.
    echo 按 Ctrl+C 停止服务
    echo ----------------------------------------
    :: 使用PowerShell包装Python，确保窗口关闭时能清理进程
    powershell -ExecutionPolicy Bypass -NoProfile -Command "$p=%PORT%;$py='%PYTHON_CMD%';$dir='%PATH_DIR%';$proc=$null;try{$proc=Start-Process -FilePath $py -ArgumentList @('-m','http.server',$p,'--directory',$dir) -PassThru -WindowStyle Hidden -ErrorAction SilentlyContinue;if(-not $proc){cd $dir;$proc=Start-Process -FilePath $py -ArgumentList @('-m','http.server',$p) -PassThru -WindowStyle Hidden};Write-Host '服务器已启动: http://localhost:'$p'/' -ForegroundColor Green;Write-Host '进程ID:' $proc.Id -ForegroundColor Gray;Write-Host '';Write-Host '按 Ctrl+C 停止服务' -ForegroundColor Yellow;try{while($proc -and -not $proc.HasExited){Start-Sleep -Milliseconds 500}}catch{}}finally{if($proc -and -not $proc.HasExited){Write-Host '';Write-Host '正在停止服务器...' -ForegroundColor Yellow;Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue;Write-Host '服务器已停止' -ForegroundColor Green}}"
    exit /b
)

:: 如果没有Python，使用PowerShell内联命令（不依赖外部文件）
echo ========================================
echo   StarForce 本地热更服务器
echo ========================================
echo.
echo 资源路径: %PATH_DIR%
echo 访问地址: http://localhost:%PORT%
echo.
echo 按 Ctrl+C 停止服务
echo ----------------------------------------
powershell -ExecutionPolicy Bypass -NoProfile -Command "$ErrorActionPreference='Stop';$p=%PORT%;$r='%PATH_DIR%';if(-not(Test-Path $r)){Write-Host '错误:路径不存在' -ForegroundColor Red;Read-Host '按回车退出';exit};$r=(Resolve-Path $r).Path;Write-Host \"服务器已启动: http://localhost:$p/\" -ForegroundColor Green;$l=New-Object System.Net.HttpListener;$l.Prefixes.Add(\"http://localhost:$p/\");try{$l.Start();[Console]::TreatControlCAsInput=$false;while($l.IsListening){try{$c=$l.GetContext();$u=$c.Request.Url.LocalPath;$f=Join-Path $r ($u -replace '/','\\');Write-Host \"$(Get-Date -Format 'HH:mm:ss') $u\" -ForegroundColor Gray;try{if(Test-Path $f -PathType Leaf){$d=[IO.File]::ReadAllBytes($f);$c.Response.OutputStream.Write($d,0,$d.Length);$c.Response.StatusCode=200}elseif(Test-Path $f -PathType Container){$b='';if($u -ne '/'){$b='<a href=..>../</a><br>'};$li=Get-ChildItem $f|ForEach-Object{$n=$_.Name;if($_.PSIsContainer){$n+='/'};'<a href='+$n+'>'+$n+'</a><br>'};$h='<html><body><h2>'+$u+'</h2>'+$b+($li -join '')+'</body></html>';$d=[Text.Encoding]::UTF8.GetBytes($h);$c.Response.ContentType='text/html;charset=utf-8';$c.Response.OutputStream.Write($d,0,$d.Length);$c.Response.StatusCode=200}else{$c.Response.StatusCode=404}}catch{$c.Response.StatusCode=500}finally{$c.Response.Close()}}catch{break}}}catch{Write-Host '错误:无法启动服务器' -ForegroundColor Red;Write-Host $_.Exception.Message -ForegroundColor Red;Read-Host '按回车退出';exit}finally{if($l.IsListening){$l.Stop()};$l.Close();Write-Host '服务器已停止' -ForegroundColor Yellow}"
