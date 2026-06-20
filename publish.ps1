# 打包三个版本

$Version = "1.0.0"


# 框架依赖版本：体积小，需要目标机器安装 .NET 运行时
Write-Host "=== 打包框架依赖版本 ===" -ForegroundColor Cyan
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue publish\dependent
dotnet publish HyperMark.Desktop\HyperMark.Desktop.csproj -c Release -r win-x64 -o publish\dependent -p:Version=$Version -p:PublishAot=false -p:SelfContained=false
Remove-Item -Path publish\dependent\*.pdb -Force -ErrorAction SilentlyContinue
Write-Host ""


# AOT 版本：原生编译，体积小、启动快
Write-Host "=== 打包 AOT 版本 ===" -ForegroundColor Cyan
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue publish\aot
dotnet publish HyperMark.Desktop\HyperMark.Desktop.csproj -c Release -r win-x64 -o publish\aot -p:Version=$Version
Remove-Item -Path publish\aot\*.pdb -Force -ErrorAction SilentlyContinue
Write-Host ""



# Self-Contained 版本：自带运行时，无需目标机器安装 .NET
Write-Host "=== 打包 Self-Contained 版本 ===" -ForegroundColor Cyan
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue publish\self-contained
dotnet publish HyperMark.Desktop\HyperMark.Desktop.csproj -c Release -r win-x64 -o publish\self-contained -p:Version=$Version -p:PublishAot=false -p:SelfContained=true -p:PublishTrimmed=true

# 删除第三方库的 PDB 文件（减小体积）
Remove-Item -Path publish\self-contained\*.pdb -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "=== 完成 ===" -ForegroundColor Green
Write-Host "AOT 版本:       publish\aot"
Write-Host "框架依赖版本:   publish\dependent"
Write-Host "Self-Contained: publish\self-contained"
