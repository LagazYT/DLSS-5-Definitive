@echo off
rem Compila o DLSS 5 Definitive com o compilador C# que ja vem no Windows (.NET Framework 4.x) e gera:
rem   DLSS 5 Definitive.exe                  (o app)
rem   dist\DLSS 5 Definitive Setup.exe       (instalador com desinstalador)
rem   dist\DLSS 5 Definitive Portable.zip    (versao portable: dados em .\Data)
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
cd /d "%~dp0"
set REFS=/r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Management.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /out:"DLSS 5 Definitive.exe" /win32manifest:app.manifest /win32icon:app.ico %REFS% src\*.cs
if errorlevel 1 (echo FALHOU: app & exit /b 1)
echo OK: DLSS 5 Definitive.exe

if not exist dist mkdir dist
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /out:"dist\DLSS 5 Definitive Setup.exe" /win32manifest:setup\setup.manifest /win32icon:app.ico %REFS% ^
  "/resource:DLSS 5 Definitive.exe,app.exe" "/resource:LEIA-ME.txt,readme.txt" ^
  setup\Setup.cs src\Lang.cs src\Common.cs src\Ui.cs src\Library.cs src\Installer.cs src\Downloader.cs src\Planner.cs src\GameScanner.cs src\RuntimeProvider.cs
if errorlevel 1 (echo FALHOU: instalador & exit /b 1)
echo OK: dist\DLSS 5 Definitive Setup.exe

set P=dist\DLSS 5 Definitive Portable
if exist "%P%" rmdir /s /q "%P%"
mkdir "%P%"
copy /y "DLSS 5 Definitive.exe" "%P%\" >nul
copy /y "LEIA-ME.txt" "%P%\" >nul
> "%P%\portable.ini" echo ; Este arquivo deixa o DLSS 5 Definitive em modo portable: configuracoes, biblioteca, capas e downloads ficam na pasta Data ao lado do exe.
if exist "dist\DLSS 5 Definitive Portable.zip" del /q "dist\DLSS 5 Definitive Portable.zip"
powershell -NoProfile -Command "Compress-Archive -Path '%P%' -DestinationPath 'dist\DLSS 5 Definitive Portable.zip'"
if errorlevel 1 (echo FALHOU: zip portable & exit /b 1)
rmdir /s /q "%P%"
echo OK: dist\DLSS 5 Definitive Portable.zip