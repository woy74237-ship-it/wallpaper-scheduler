# 紹介ページ・記事用のスクリーンショットを docs\images\ に撮る（docs\images\ は手元だけの素材）
#   main.png / edit.png
# サンプルの壁紙（promo\wallpapers の朝・昼・夜。先に tools\make-promo.ps1 を実行しておく）を
# 6:00・12:00・19:00 に登録した画面を撮影します。実際の壁紙や設定は変えません。
# 画面に出す「今の時刻」は 14:12（昼の壁紙が表示中）に固定して撮るので、いつ実行しても同じ見た目になります。
#   -At "HH:mm" で時刻を変えられます（例: -At 21:30 で夜の壁紙が表示中の画面）
# 窓だけを撮るので、窓の外のデスクトップは写り込みません。配色は Windows のダーク／ライトモードに従います。
param([string]$At = '14:12')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tmp = Join-Path ([IO.Path]::GetTempPath()) 'WallpaperScheduler-Screenshots.exe'
$out = Join-Path $root 'docs\images'
$wallpapers = Join-Path $root 'promo\wallpapers'
if (-not (Test-Path (Join-Path $wallpapers 'noon.jpg'))) { throw "サンプルの壁紙がありません。先に tools\make-promo.ps1 を実行してください: $wallpapers" }
New-Item -ItemType Directory -Force $out | Out-Null

$src = @((Join-Path $PSScriptRoot 'Screenshots.cs')) + (Get-ChildItem (Join-Path $root 'src') -Filter *.cs).FullName
& $csc /nologo /target:winexe /codepage:65001 /main:Screenshots `
    "/win32manifest:$root\assets\app.manifest" "/resource:$root\assets\app.ico,WallpaperScheduler.app.ico" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.Serialization.dll /r:System.Core.dll `
    "/out:$tmp" $src
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Start-Process $tmp -ArgumentList "`"$out`"", "`"$wallpapers`"", $At -Wait
Remove-Item $tmp
Get-ChildItem $out -Filter *.png | ForEach-Object { '{0,-12} {1,8:N0} bytes' -f $_.Name, $_.Length }
