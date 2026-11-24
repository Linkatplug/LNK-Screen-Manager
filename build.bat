@echo off
rem LNK Screen Manager - compilation (utilise le compilateur C# inclus dans Windows)
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set ROOT=%~dp0
set REFS=/r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll
if not exist "%ROOT%build" mkdir "%ROOT%build"
if not exist "%ROOT%assets" mkdir "%ROOT%assets"

rem 1) passe sans icone, qui dessine l'icone et le logo
"%CSC%" /nologo /target:winexe /optimize /out:"%ROOT%build\stage1.exe" %REFS% "%ROOT%src\*.cs" || exit /b 1
start "" /wait "%ROOT%build\stage1.exe" --make-icon "%ROOT%assets" || exit /b 1

rem 2) exe final avec icone integree
"%CSC%" /nologo /target:winexe /optimize /win32icon:"%ROOT%assets\app.ico" /resource:"%ROOT%assets\app.ico",LnkScreenManager.app.ico /out:"%ROOT%LNK Screen Manager.exe" %REFS% "%ROOT%src\*.cs" || exit /b 1
echo Build OK : "%ROOT%LNK Screen Manager.exe"
