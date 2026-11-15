# 壁紙スケジューラのビルド（Windows 標準の .NET Framework 4.x の csc.exe を使用）
$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe が見つかりません: $csc" }

$out = Join-Path $PSScriptRoot 'bin'
$assets = Join-Path $PSScriptRoot 'assets'
New-Item -ItemType Directory -Force $out | Out-Null

# /win32icon: exe ファイルのアイコン、/resource: トレイ・ウィンドウ用に同じアイコンを埋め込み
& $csc /nologo /target:winexe /codepage:65001 /optimize+ `
    "/out:$out\WallpaperScheduler.exe" `
    "/win32icon:$assets\app.ico" "/win32manifest:$assets\app.manifest" `
    "/resource:$assets\app.ico,WallpaperScheduler.app.ico" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll /r:System.Core.dll `
    (Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs).FullName
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "ビルド完了: $out\WallpaperScheduler.exe"
