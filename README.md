# 壁紙スケジューラ

指定した時刻に Windows の壁紙を自動で切り替えるタスクトレイ常駐アプリです。

🌐 紹介ページ: **https://woy74237-ship-it.github.io/wallpaper-scheduler/**

📝 紹介記事
- 使い方の紹介（note）: [朝と夜で壁紙が自動で変わる！無料の Windows アプリ「壁紙スケジューラ」を作りました](https://note.com/quiet_raven5890/n/nb744c1f870e7)
- 作り方の解説（Zenn）: [時刻で壁紙が自動で切り替わる Windows アプリを、標準の csc.exe だけで作った](https://zenn.dev/walt_owen/articles/3291d8d844a05d)

## ダウンロード

**[最新版をダウンロード（GitHub Releases）](https://github.com/woy74237-ship-it/wallpaper-scheduler/releases/latest)**

`WallpaperScheduler-v〇.〇.〇.zip` を展開して `WallpaperScheduler.exe` を起動するだけで使えます（インストール不要・Windows 10 / 11 対応）。
初回起動時に「Windows によって PC が保護されました」と表示された場合は、「詳細情報」→「実行」で起動できます。

## 開発の支援

無料でお使いいただけます。気に入っていただけたら、[OFUSE](https://ofuse.me/c0fdccc4) から応援していただけるとうれしいです。
「こんな機能がほしい」といった要望も、[Issues](https://github.com/woy74237-ship-it/wallpaper-scheduler/issues) でお待ちしています。

## ビルド

追加のインストールは不要です（Windows 標準の .NET Framework 4.x の `csc.exe` を使用）。

```powershell
.\build.ps1
```

`bin\WallpaperScheduler.exe` が生成されます。

## 配布用 ZIP の作成

```powershell
.\package.ps1
```

`dist\WallpaperScheduler-v<バージョン>.zip`（exe と利用者向けの `README.txt`）と、
改ざん確認用の `.zip.sha256` が生成されます。

リリースの手順:

1. `src/Core.cs` 先頭のバージョン（3 か所）を上げる
   - 支援ページ・公式ページの URL も同じファイルの `AppInfo.SupportUrl` / `WebsiteUrl` で設定します（空欄ならボタン非表示）
2. `.\package.ps1` を実行
3. コミットしてタグを付ける（例: `git tag v1.3`）

アイコンのデザインを変えたときは `tools/MakeIcon.cs` を編集し、`.\tools\make-icon.ps1` で
`assets/app.ico` を再生成します。支援ページや SNS 用の画像（プロフィール・カバー・OGP）は
`.\tools\make-promo.ps1` で `promo/` に出力されます。

紹介ページは `site/` にあり、main ブランチに push すると GitHub Actions（`.github/workflows/pages.yml`）で
GitHub Pages に公開されます。ページ用の画像は `.\tools\make-site-assets.ps1` で `site/images/` に生成します。

## 使い方

1. `WallpaperScheduler.exe` を起動（初回は設定画面が開きます）
2. 「追加」で時刻と画像を登録（画面に画像をドラッグ＆ドロップしても追加できます）
3. 表示方法（ページ幅に合わせる／画面に合わせる など）を選ぶ

変更はその場で保存・反映されます。

- 各時刻になると、その画像に切り替わり、次の時刻まで維持されます（毎日繰り返し）。
- 最初の時刻より前（深夜など）は、前日の最後の画像が使われます。
- 画面上部のタイムラインで、24 時間のどの時間帯にどの壁紙が表示されるかを確認できます。
- Windows のダーク／ライトモードとアクセントカラーに合わせて配色が変わります。
- タスクトレイのアイコン: クリックで設定、右クリックで「今すぐ適用」「自動切り替え」「終了」。
- 起動中に exe をもう一度実行すると、設定画面が開きます。
- スリープ復帰・ロック解除・システム時刻の変更時にも再チェックします。
- 「Windows の起動時に開始」で自動起動を設定できます（`HKCU\...\Run` に `"exe のパス" --autostart` で登録）。
  自動起動のときは画面を出さずにトレイに常駐し、exe を手動で起動したときは設定画面を開きます。
- 自動起動の登録先の exe が見つからない場合は、起動した exe に自動で登録し直します。
- Windows の設定（スタートアップ アプリ）で自動起動がオフにされている場合は、設定画面に案内を表示します。アプリのスイッチでオンにすると、Windows 側のオフも解除します。
- Windows 11 ではトレイのアイコンが最初「^」の中に隠れます。常に表示するには、タスクバーの設定 →「その他のシステム トレイ アイコン」でオンにします。

設定ファイル: `%APPDATA%\WallpaperScheduler\settings.json`

## ソース構成

| ファイル | 内容 |
| --- | --- |
| `src/Core.cs` | 設定データ・スケジュール計算・壁紙設定・自動起動 |
| `src/Theme.cs` | 配色・フォント・描画ヘルパー・サムネイルキャッシュ |
| `src/Controls.cs` | 自前描画のコントロール（ボタン、トグル、カード、タイムラインなど） |
| `src/MainForm.cs` | メイン画面 |
| `src/Dialogs.cs` | 追加・編集ダイアログ、確認ダイアログ |
| `src/App.cs` | タスクトレイ常駐部とエントリポイント |
| `assets/app.ico` | アプリアイコン（`tools/make-icon.ps1` で生成） |
| `assets/app.manifest` | 高 DPI 対応・管理者権限不要などの宣言 |
| `assets/README.txt` | 配布 ZIP に同梱する利用者向けの説明 |

## バージョン

- v1.3.2: 手動起動時は設定画面を開き、自動起動時はトレイに常駐。自動起動の登録先が消えていたら自動で修復。Windows の設定で自動起動がオフのときに案内を表示
- v1.3.1: 「このアプリについて」に紹介記事（公式ページ）へのボタンを追加
- v1.3: 「このアプリについて」画面と開発の支援リンクを追加
- v1.2: 配布の仕上げ（専用アイコン、バージョン情報、マニフェスト、配布用 ZIP スクリプト）
- v1.1: UI を刷新（Windows 11 風デザイン、ダークモード、タイムライン、サムネイル付きカード）
- v1.0: 最初のバージョン（`git checkout v1.0` で取り出せます）

## ライセンス

[MIT License](LICENSE)
