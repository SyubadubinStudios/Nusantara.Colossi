# Nusantara Colossi - Windows packaging.
# Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.
#
# Publishes the desktop head self-contained for win-x64 and zips it.
# Usage: pwsh build/package-windows.ps1 [-Configuration Release] [-OutDir <repo>/artifacts]
# Works in Windows PowerShell 5.1 and PowerShell 7.
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$OutDir = ''
)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $root 'artifacts' }
$project = Join-Path $root 'src/NusantaraColossi.Desktop/NusantaraColossi.Desktop.csproj'

$version = (& dotnet msbuild $project -getProperty:Version | Out-String).Trim()
if (-not $version) { $version = '0.0.0' }

$publishDir = Join-Path $OutDir "publish/$Runtime"
$stageRoot = Join-Path $OutDir "windows"
$stageDir = Join-Path $stageRoot 'NusantaraColossi'
$packages = Join-Path $OutDir 'packages'

Write-Host "==> Publishing $Runtime ($Configuration, v$version)"
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
& dotnet publish $project -c $Configuration -r $Runtime --self-contained -o $publishDir -nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

foreach ($f in @('NusantaraColossi.exe', 'threenet_core.dll', 'libSkiaSharp.dll', 'av_libglesv2.dll', 'assets/art/icon.png')) {
    if (-not (Test-Path (Join-Path $publishDir $f))) { throw "$f missing from publish output" }
}

Write-Host '==> Staging'
if (Test-Path $stageRoot) { Remove-Item -Recurse -Force $stageRoot }
New-Item -ItemType Directory -Force $stageDir | Out-Null
Copy-Item -Recurse -Path (Join-Path $publishDir '*') -Destination $stageDir
# Managed debug symbols are not needed in a player build.
Get-ChildItem $stageDir -Filter *.pdb | Remove-Item -Force

New-Item -ItemType Directory -Force $packages | Out-Null
$zip = Join-Path $packages "NusantaraColossi-$version-$Runtime.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Write-Host "==> Zipping $zip"
Compress-Archive -Path $stageDir -DestinationPath $zip -CompressionLevel Optimal

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "==> Done: $zip ($mb MB)"

# Installer (Inno Setup 6), when ISCC is available: PATH, the per-user install, or Program Files.
$iscc = (Get-Command iscc -ErrorAction SilentlyContinue).Source
if (-not $iscc) {
    $iscc = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if ($iscc) {
    Write-Host "==> Building installer with $iscc"
    $script = Join-Path $root 'build/installer/NusantaraColossi.iss'
    & $iscc "/DAppVersion=$version" "/DSourceDir=$stageDir" "/DOutputDir=$packages" "/DRepoRoot=$root" $script
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }
    $setup = Join-Path $packages "NusantaraColossi-$version-$Runtime-setup.exe"
    if (-not (Test-Path $setup)) { throw "installer not produced: $setup" }
    $mb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
    Write-Host "==> Done: $setup ($mb MB)"
}
else { Write-Host '==> Inno Setup not found; skipping the installer (zip only).' }
