param(
    # Folder of the original CodingPass download (contains CodingPass.Judge.exe).
    [Parameter(Mandatory = $true)][string]$CodingPassDir,
    # Known-good Code::Blocks 25.03 MinGW to bundle.
    [string]$MinGWDir = 'C:\Program Files\CodeBlocks\MinGW',
    [string]$Iscc
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$payload = Join-Path $root 'build\payload'
if (-not (Test-Path -LiteralPath (Join-Path $CodingPassDir 'CodingPass.Judge.exe'))) { throw "CodingPass.Judge.exe not found in $CodingPassDir" }
if (-not (Test-Path -LiteralPath (Join-Path $MinGWDir 'bin\g++.exe'))) { throw "g++.exe not found in $MinGWDir\bin" }
if (-not $Iscc) {
    $Iscc = @((Join-Path $root 'tools\InnoSetup\ISCC.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $Iscc) { throw 'Inno Setup 6 (ISCC.exe) not found. Install it or pass -Iscc.' }
}
if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload -Recurse -Force }
New-Item -ItemType Directory -Path $payload -Force | Out-Null
& (Join-Path $PSScriptRoot 'Build-Tray.ps1') -OutputPath (Join-Path $payload 'CodingPassLocalServer.exe')
if ($LASTEXITCODE -ne 0) { throw 'Tray build failed' }
# Explicit file allowlist: no Exam, user state, browser profile, history or logs.
$files = @('CodingPass.Judge.exe','CodingPass.Judge.dll','CodingPass.Judge.deps.json','CodingPass.Judge.runtimeconfig.json',
    'CodingPass.Webview.exe','CodingPass.Webview.dll','CodingPass.Webview.deps.json','CodingPass.Webview.runtimeconfig.json',
    'CodingPass.Archive.exe','CodingPass.ebk','Newtonsoft.Json.dll','System.Management.dll')
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $CodingPassDir $file) -Destination $payload -Force }
foreach ($file in Get-ChildItem -LiteralPath $CodingPassDir -Filter 'Microsoft.*.dll' -File) {
    Copy-Item -LiteralPath $file.FullName -Destination $payload -Force
}
Copy-Item -LiteralPath (Join-Path $CodingPassDir 'runtimes') -Destination $payload -Recurse -Force
& $Iscc "/DMinGWSource=$MinGWDir" (Join-Path $root 'installer\CodingPassLocalServer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
