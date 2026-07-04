$ErrorActionPreference = "Stop"

$workspaceRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location "$workspaceRoot\.."

if (-not (Test-Path "bin")) {
    New-Item -ItemType Directory -Path "bin" | Out-Null
}

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

Write-Host "正在编译 volume-osd.exe..." -ForegroundColor Cyan
& $csc /nologo /target:winexe /out:bin\volume-osd.exe /lib:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF /reference:PresentationFramework.dll /reference:PresentationCore.dll /reference:WindowsBase.dll /reference:System.Xaml.dll src\app-main.cs src\osd-window.cs src\audio-manager.cs

Write-Host "正在编译 get-active-app.exe..." -ForegroundColor Cyan
& $csc /nologo /target:exe /out:bin\get-active-app.exe src\get-active-app.cs

Write-Host "编译成功！所有 exe 已放入 bin 文件夹。" -ForegroundColor Green
pause
