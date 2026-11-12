$ErrorActionPreference = 'Stop'
# Build AlapSB pakai compiler C# bawaan Windows (.NET Framework 4.x) — tanpa install apa pun.
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw 'csc.exe (.NET Framework 4) tidak ditemukan.' }

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }

$ico = Join-Path $root 'assets\alapsb.ico'
$icoParam = if (Test-Path $ico) { "/win32icon:$ico" } else { "" }

$resParams = @()
$musicAssets = Join-Path $root 'assets\music'
if (Test-Path $musicAssets) {
    Get-ChildItem $musicAssets -Filter *.mp3 | ForEach-Object {
        $resName = "AlapSB.Music." + $_.Name
        $resParams += "/resource:$($_.FullName),$resName"
    }
}

& $csc /nologo /target:winexe /optimize+ /platform:anycpu /codepage:65001 `
    "/out:$dist\AlapSB.exe" "/win32manifest:$root\src\app.manifest" $icoParam $resParams `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll `
    $sources
if ($LASTEXITCODE -ne 0) { throw "Build gagal ($LASTEXITCODE)" }
$size = [math]::Round((Get-Item "$dist\AlapSB.exe").Length / 1KB)
Write-Host "Build OK -> dist\AlapSB.exe ($size KB)"
