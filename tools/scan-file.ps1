# Microsoft Defender でファイルをスキャンする（ファイルの削除や隔離はしない）
# 使い方: .\tools\scan-file.ps1 <ファイルのパス>
# 終了コード: 0 = 検出なし / 1 = 検出あり / 2 = スキャンできなかった
param([Parameter(Mandatory = $true)][string]$Path)

$mp = Join-Path $env:ProgramFiles 'Windows Defender\MpCmdRun.exe'
if (-not (Test-Path $mp)) {
    Write-Warning "Microsoft Defender が見つからないため、スキャンできませんでした: $Path"
    exit 2
}
if (-not (Test-Path $Path)) {
    Write-Warning "ファイルが見つかりません: $Path"
    exit 2
}

# 最新の定義ファイルで判定するため、可能なら更新しておく（失敗しても続行）
& $mp -SignatureUpdate *> $null

$out = & $mp -Scan -ScanType 3 -File (Resolve-Path $Path).Path -DisableRemediation 2>&1 | Out-String
$threats = [regex]::Matches($out, 'Threat\s*:\s*(.+)') | ForEach-Object { $_.Groups[1].Value.Trim() } | Select-Object -Unique

if ($threats) {
    Write-Host ""
    Write-Host "×  Microsoft Defender が検出しました: $Path" -ForegroundColor Red
    $threats | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
    exit 1
}
if ($out -match 'found no threats') {
    Write-Host "✓  Microsoft Defender のスキャン: 検出なし ($Path)" -ForegroundColor Green
    exit 0
}
Write-Warning "スキャン結果を判定できませんでした: $Path`n$out"
exit 2
