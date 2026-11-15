# 配布用 ZIP を作成する
#   dist\WallpaperScheduler-v<バージョン>.zip        … exe と README.txt
#   dist\WallpaperScheduler-v<バージョン>.zip.sha256 … 改ざん確認用のハッシュ
# バージョンは src\Core.cs の AssemblyInformationalVersion から自動で取得します。
# 作成した ZIP は Microsoft Defender でスキャンし、検出された場合は ZIP を削除して中止します。
#   -SkipScan … スキャンを省略する（Defender が使えない環境用。公開前には必ず別途確認すること）
param([switch]$SkipScan)
$ErrorActionPreference = 'Stop'

& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Join-Path $PSScriptRoot 'bin\WallpaperScheduler.exe'
$version = (Get-Item $exe).VersionInfo.ProductVersion
$name = "WallpaperScheduler-v$version"
$dist = Join-Path $PSScriptRoot 'dist'
$stage = Join-Path $dist $name
$zip = Join-Path $dist "$name.zip"

if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item $exe $stage
Copy-Item (Join-Path $PSScriptRoot 'assets\README.txt') $stage

if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Remove-Item -Recurse -Force $stage

# ウイルス対策ソフトに誤検出されるものを公開しないよう、公開前にスキャンする
if (-not $SkipScan) {
    & (Join-Path $PSScriptRoot 'tools\scan-file.ps1') $zip
    $scan = $LASTEXITCODE
    if ($scan -eq 1) {
        Remove-Item $zip, "$zip.sha256" -ErrorAction SilentlyContinue
        Write-Host ""
        Write-Host "ZIP を削除して中止しました。このまま公開しないでください。" -ForegroundColor Red
        Write-Host "直前の変更が原因の可能性があります。変更を見直すか、Microsoft に誤検出を報告してください。" -ForegroundColor Red
        exit 1
    }
    if ($scan -ne 0) {
        Write-Host "スキャンで確認できませんでした。公開前に、別の方法でウイルス対策の確認をしてください。" -ForegroundColor Yellow
    }
}

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $name.zip" | Set-Content -Encoding ascii "$zip.sha256"

Write-Host ""
Write-Host "配布用 ZIP を作成しました: $zip"
Write-Host "SHA-256: $hash"
