# 打包两个版本

# 框架依赖版本：体积小，需要目标机器安装 .NET 运行时
Write-Host "=== 打包框架依赖版本 ===" -ForegroundColor Cyan
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue publish\dependent  # 清空旧的发布目录
dotnet publish HyperMark.Desktop\HyperMark.Desktop.csproj -c Release -o publish\dependent

Write-Host ""

# Self-Contained 版本：体积大，自带运行时，无需目标机器安装 .NET
Write-Host "=== 打包 Self-Contained 版本 ===" -ForegroundColor Cyan
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue publish\self-contained  # 清空旧的发布目录
dotnet publish HyperMark.Desktop\HyperMark.Desktop.csproj -c Release --self-contained true -o publish\self-contained

Write-Host ""
Write-Host "=== 完成 ===" -ForegroundColor Green
Write-Host "框架依赖版本: publish\dependent"
Write-Host "Self-Contained 版本: publish\self-contained"
