# 配布用 ZIP を作成する
#   dist\WallpaperScheduler-v<バージョン>.zip        … exe と README.txt
#   dist\WallpaperScheduler-v<バージョン>.zip.sha256 … 改ざん確認用のハッシュ
# バージョンは src\Core.cs の AssemblyInformationalVersion から自動で取得します。
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

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $name.zip" | Set-Content -Encoding ascii "$zip.sha256"

Write-Host ""
Write-Host "配布用 ZIP を作成しました: $zip"
Write-Host "SHA-256: $hash"
