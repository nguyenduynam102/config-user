# Run as Administrator. Contains no credentials.
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Hay chay Install.cmd bang Run as administrator.' }
$source = Join-Path $PSScriptRoot 'WinUser.exe'
if (-not (Test-Path -LiteralPath $source)) { throw 'Khong tim thay WinUser.exe; chay Build.cmd truoc va dung thu muc artifacts\win-x64.' }
$destination = Join-Path $env:ProgramFiles 'WinUser'
$service = Get-Service -Name WinUserBroker -ErrorAction SilentlyContinue
# Refuse to overwrite running logon agents rather than terminating user processes.
$brokerPid = if ($service) { (Get-CimInstance Win32_Service -Filter "Name='WinUserBroker'").ProcessId } else { 0 }
$active = Get-Process -Name WinUser -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $brokerPid }
if ($active) { throw 'Dong WinUser va dang xuat cac phien Agent truoc khi cap nhat. Khong tu dong dung chuong trinh cua user.' }
if ($service) { Stop-Service WinUserBroker -ErrorAction Stop }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$item = Get-Item -LiteralPath $destination
if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Tu choi thu muc cai dat reparse point.' }
$acl = New-Object Security.AccessControl.DirectorySecurity
$acl.SetAccessRuleProtection($true, $false)
foreach ($id in @('S-1-5-18','S-1-5-32-544')) {
 $sid = New-Object Security.Principal.SecurityIdentifier($id)
 $rule = New-Object Security.AccessControl.FileSystemAccessRule($sid, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
 $acl.AddAccessRule($rule)
}
$readers = New-Object Security.Principal.SecurityIdentifier('S-1-5-32-545')
$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule($readers, 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow')))
Set-Acl -LiteralPath $destination -AclObject $acl
$exe = Join-Path $destination 'WinUser.exe'
if ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($exe)) { Copy-Item -LiteralPath $source -Destination $exe -Force }
$binaryPath = '"' + $exe + '" --broker'
if ($service) {
 $result = Invoke-CimMethod -InputObject (Get-CimInstance Win32_Service -Filter "Name='WinUserBroker'") -MethodName Change -Arguments @{ PathName = $binaryPath; StartMode = 'Automatic' }
 if ($result.ReturnValue -ne 0) { throw "Service change failed: $($result.ReturnValue)" }
} else { New-Service -Name WinUserBroker -DisplayName 'WinUser Credential Broker' -BinaryPathName $binaryPath -StartupType Automatic | Out-Null }
& sc.exe failure WinUserBroker reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Cannot configure recovery: $LASTEXITCODE" }
New-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run' -Name WinUserNasAgent -Value ('"' + $exe + '" --agent') -PropertyType String -Force | Out-Null
Start-Service WinUserBroker
(Get-Service WinUserBroker).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
Write-Host "Da cai $exe va dich vu WinUserBroker. Agent tu chay o lan dang nhap tiep theo."
Write-Host 'Chua xac minh ket noi NAS hoac Windows/RDP. Xem WINDOWS-VALIDATION.vi.md.'
