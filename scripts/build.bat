@echo off
setlocal

cd /d "%~dp0\.."

if not exist bin mkdir bin

set CSC="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

echo 正在编译 volume-osd.exe...
%CSC% /nologo /target:winexe /out:bin\volume-osd.exe /lib:C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF /reference:PresentationFramework.dll /reference:PresentationCore.dll /reference:WindowsBase.dll /reference:System.Xaml.dll src\app-main.cs src\osd-window.cs src\audio-manager.cs
if %errorlevel% neq 0 (
    echo volume-osd.exe 编译失败
    exit /b %errorlevel%
)

echo 正在编译 get-active-app.exe...
%CSC% /nologo /target:exe /out:bin\get-active-app.exe src\get-active-app.cs
if %errorlevel% neq 0 (
    echo get-active-app.exe 编译失败
    exit /b %errorlevel%
)

echo 编译成功！所有 exe 已放入 bin 文件夹。
pause
