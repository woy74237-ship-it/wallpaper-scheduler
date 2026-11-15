# 紹介サイト（site/）用の画像を生成・コピーする
#   使う素材: assets\app.ico、docs\images\*.png（スクリーンショット）、promo\wallpapers\*.jpg、promo\ogp.png
#   ※ docs\ と promo\ は手元だけの素材です。先に tools\make-promo.ps1 を実行しておいてください。
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'site\images'
New-Item -ItemType Directory -Force $out | Out-Null
Add-Type -AssemblyName System.Drawing

function Save-Resized([string]$src, [string]$dst, [int]$w, [int]$h) {
    $img = [System.Drawing.Image]::FromFile($src)
    try {
        $bmp = New-Object System.Drawing.Bitmap $w, $h
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = 'HighQualityBicubic'; $g.PixelOffsetMode = 'HighQuality'; $g.SmoothingMode = 'HighQuality'
        $g.DrawImage($img, 0, 0, $w, $h); $g.Dispose()
        if ($dst -like '*.jpg') {
            $enc = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }
            $p = New-Object System.Drawing.Imaging.EncoderParameters 1
            $p.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 85L
            $bmp.Save($dst, $enc, $p)
        } else { $bmp.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png) }
        $bmp.Dispose()
    } finally { $img.Dispose() }
}

# アイコン（256px はアイコン生成ツールのプレビュー出力を使う）
& (Join-Path $PSScriptRoot 'make-icon.ps1') -Preview (Join-Path $out 'icon-256.png') | Out-Null
Save-Resized (Join-Path $out 'icon-256.png') (Join-Path $out 'apple-touch-icon.png') 180 180
$ico = New-Object System.Drawing.Icon (Join-Path $root 'assets\app.ico'), 32, 32
$ico.ToBitmap().Save((Join-Path $out 'favicon-32.png'), [System.Drawing.Imaging.ImageFormat]::Png); $ico.Dispose()

# スクリーンショット
Copy-Item (Join-Path $root 'docs\images\main.png') $out -Force
Copy-Item (Join-Path $root 'docs\images\edit.png') $out -Force

# サンプル壁紙（朝・昼・夜）の縮小版
foreach ($n in 'morning', 'noon', 'night') {
    Save-Resized (Join-Path $root "promo\wallpapers\$n.jpg") (Join-Path $out "$n.jpg") 960 540
}

# シェア用画像とヒーロー背景
Copy-Item (Join-Path $root 'promo\ogp.png') $out -Force
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tmp = Join-Path ([IO.Path]::GetTempPath()) 'WallpaperScheduler-MakeHero.exe'
& $csc /nologo /codepage:65001 /r:System.Drawing.dll "/out:$tmp" (Join-Path $PSScriptRoot 'MakeIcon.cs')
& $tmp --cover (Join-Path $out 'hero.png') 1920 1080
Remove-Item $tmp

Get-ChildItem $out | ForEach-Object { '{0,-22} {1,8:N0} bytes' -f $_.Name, $_.Length }
