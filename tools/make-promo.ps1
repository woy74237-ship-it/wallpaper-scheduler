# 支援ページ・SNS 用の画像を promo\ に生成する
#   ofuse-avatar.png  … プロフィール画像（512x512、丸い切り抜きに対応）
#   ofuse-cover.png   … カバーアート（1000x150）
#   ogp.png           … シェア時のサムネイル（1200x630）
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tmp = Join-Path ([IO.Path]::GetTempPath()) 'WallpaperScheduler-MakePromo.exe'

& $csc /nologo /codepage:65001 /r:System.Drawing.dll "/out:$tmp" (Join-Path $PSScriptRoot 'MakeIcon.cs')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$out = Join-Path $root 'promo'
New-Item -ItemType Directory -Force $out | Out-Null
& $tmp --avatar (Join-Path $out 'ofuse-avatar.png') 512
& $tmp --cover (Join-Path $out 'ofuse-cover.png') 1000 150
& $tmp --ogp (Join-Path $out 'ogp.png')
Remove-Item $tmp
Get-ChildItem $out -Filter *.png | ForEach-Object { Write-Host "生成しました: $($_.FullName)" }
