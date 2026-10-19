# Tailwind CLI 下载脚本（Windows）
$url = "https://github.com/tailwindlabs/tailwindcss/releases/latest/download/tailwindcss-windows-x64.exe"
$target = "./tailwindcss.exe"

Write-Host "正在从 $url 下载 Tailwind CLI..." -ForegroundColor Cyan
Invoke-WebRequest -Uri $url -OutFile $target

Write-Host "完成！二进制文件位于 tools/tailwindcss.exe" -ForegroundColor Green
