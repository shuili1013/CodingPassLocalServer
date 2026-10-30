param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$src = Join-Path $root 'src'
$build = Join-Path $root 'build'
New-Item -ItemType Directory -Path $build -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$destination = if ($OutputPath) { $OutputPath } else { Join-Path $build 'CodingPassLocalServer.exe' }
# TrayArtwork renders the .ico and menu/icon previews next to itself.
$artwork = Join-Path $build 'TrayArtwork.exe'
& $compiler /nologo /codepage:65001 /target:exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$artwork" (Join-Path $src 'TrayArtwork.cs') (Join-Path $src 'TrayAppearance.cs')
if ($LASTEXITCODE -ne 0) { throw 'Artwork build failed.' }
& $artwork
if ($LASTEXITCODE -ne 0) { throw 'Artwork generation failed.' }
$iconFile = Join-Path $build 'CodingPassLocalServer.ico'
& $compiler /nologo /codepage:65001 /target:winexe /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/win32icon:$iconFile" "/out:$destination" (Join-Path $src 'LocalServerTray.cs') (Join-Path $src 'TrayAppearance.cs')
if ($LASTEXITCODE -ne 0) { throw 'Tray build failed.' }
