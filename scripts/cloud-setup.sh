#!/usr/bin/env bash
set -euo pipefail
cd /workspace/config-user
sdk_root=/workspace/tools/dotnet
export DOTNET_CLI_HOME=/workspace/tools/dotnet-home
export DOTNET_CLI_TELEMETRY_OPTOUT=1
mkdir -p "$sdk_root"
# Verify an official SDK archive against SHA-512 from Microsoft's HTTPS release metadata.
if ! "$sdk_root/dotnet" --list-sdks 2>/dev/null | rg -q '^10\.0\.402 '; then
  download_dir=$(mktemp -d /tmp/winuser-sdk.XXXXXX)
  trap 'rm -rf "$download_dir"' EXIT
  curl --fail --silent --show-error --location https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json -o "$download_dir/releases.json"
  python3 - "$download_dir" <<'PY'
import json, pathlib, sys, urllib.request, hashlib, tarfile
folder=pathlib.Path(sys.argv[1])
metadata=json.loads((folder/'releases.json').read_text())
files=[f for r in metadata['releases'] for sdk in r.get('sdks',[]) if sdk['version']=='10.0.402' for f in sdk['files'] if f['rid']=='linux-x64' and f['name'].endswith('.tar.gz')]
if len(files)!=1: raise SystemExit('Cannot identify official SDK artifact')
f=files[0]
if not f['url'].startswith('https://builds.dotnet.microsoft.com/'): raise SystemExit('Unexpected SDK origin')
archive=folder/'sdk.tar.gz'
with urllib.request.urlopen(f['url'], timeout=120) as response, archive.open('wb') as output:
    while block:=response.read(1024*1024): output.write(block)
if hashlib.sha512(archive.read_bytes()).hexdigest().lower()!=f['hash'].lower(): raise SystemExit('SDK SHA-512 mismatch')
with tarfile.open(archive) as tar: tar.extractall('/workspace/tools/dotnet',filter='data')
PY
fi
"$sdk_root/dotnet" run --project tests/PolicyChecks -c Release
"$sdk_root/dotnet" publish src/WinUser/WinUser.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/win-x64
cp Install.cmd Install.ps1 Uninstall.cmd README.md docs/WINDOWS-VALIDATION.vi.md artifacts/win-x64/
