# アプリアイコン assets\app.ico を再生成する（デザインを変えたときだけ実行）
# 使い方: .\tools\make-icon.ps1 [-Preview preview.png] [-Avatar avatar.png]
#   -Avatar: SNS・支援サービスのプロフィール用画像（512x512、丸い切り抜きに対応）も出力
param([string]$Preview, [string]$Avatar)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tmp = Join-Path ([IO.Path]::GetTempPath()) 'WallpaperScheduler-MakeIcon.exe'

& $csc /nologo /r:System.Drawing.dll "/out:$tmp" (Join-Path $PSScriptRoot 'MakeIcon.cs')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

New-Item -ItemType Directory -Force (Join-Path $root 'assets') | Out-Null
$ico = Join-Path $root 'assets\app.ico'
if ($Preview) { & $tmp $ico $Preview } else { & $tmp $ico }
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if ($Avatar) { & $tmp --avatar $Avatar 512; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }
Remove-Item $tmp
Write-Host "アイコンを生成しました: $ico"
