@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
 echo Can cai .NET SDK 8 hoac moi hon. Khong can Visual Studio IDE.
 exit /b 1
)
dotnet run --project tests\PolicyChecks -c Release
if errorlevel 1 exit /b 1
dotnet publish src\WinUser\WinUser.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts\win-x64
if errorlevel 1 exit /b 1
copy /y Install.cmd artifacts\win-x64\ >nul
copy /y Install.ps1 artifacts\win-x64\ >nul
copy /y Uninstall.cmd artifacts\win-x64\ >nul
copy /y README.md artifacts\win-x64\ >nul
copy /y docs\WINDOWS-VALIDATION.vi.md artifacts\win-x64\ >nul
echo Build thanh cong: artifacts\win-x64\WinUser.exe
exit /b 0
