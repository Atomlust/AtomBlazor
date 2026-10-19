#!/bin/bash
# Tailwind CLI 下载脚本（Linux/macOS，多架构）

ARCH=$(uname -m)
OS=$(uname -s)

if [ "$OS" == "Linux" ]; then
    TARGET="tailwindcss-linux"
    if [ "$ARCH" == "x86_64" ]; then
        PLATFORM="linux-x64"
    elif [[ "$ARCH" == "aarch64" || "$ARCH" == "arm64" ]]; then
        PLATFORM="linux-arm64"
    else
        echo "不支持的 Linux 架构：$ARCH"
        exit 1
    fi
elif [ "$OS" == "Darwin" ]; then
    TARGET="tailwindcss-macos"
    if [ "$ARCH" == "arm64" ]; then
        PLATFORM="macos-arm64"
    else
        PLATFORM="macos-x64"
    fi
else
    echo "不支持的操作系统：$OS"
    exit 1
fi

URL="https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-$PLATFORM"

echo "正在从 $URL 下载..."
curl -L "$URL" -o "./$TARGET"
chmod +x "./$TARGET"
echo "完成！二进制文件位于 tools/$TARGET"
