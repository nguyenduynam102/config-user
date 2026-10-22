@echo off
rem Run as Administrator. Keep application data and NAS/user files.
powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; Remove-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run' -Name WinUserNasAgent -ErrorAction SilentlyContinue; Stop-Service WinUserBroker -ErrorAction SilentlyContinue"
if errorlevel 1 exit /b 1
sc.exe delete WinUserBroker
echo Khong xoa user, profile, du lieu NAS, thu muc rieng hoac %%ProgramData%%\WinUser.
echo Agent dang chay se dung khi user dang xuat. Tu xoa thu muc Program Files\WinUser sau khi dong cac tien trinh.
