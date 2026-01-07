#!/bin/bash

echo "========================================"
echo "  StarForce 本地热更服务器"
echo "========================================"
echo ""

PORT=8080
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PATH_DIR="$SCRIPT_DIR/Build"

echo "资源路径: $PATH_DIR"
echo "访问地址: http://localhost:$PORT"
echo ""
echo "按 Ctrl+C 停止服务"
echo "----------------------------------------"

# 检查Python是否可用
if command -v python3 &> /dev/null; then
    python3 -m http.server $PORT --directory "$PATH_DIR"
elif command -v python &> /dev/null; then
    python -m http.server $PORT --directory "$PATH_DIR"
else
    echo "未找到Python，请安装Python"
    echo "Mac: brew install python3"
    echo "Linux: sudo apt install python3"
    exit 1
fi

