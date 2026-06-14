# OMS

[简体中文](README.md) | [English](README.en.md) | **日本語**

> BMS と osu!mania のための Windows 向け音楽ゲームクライアント。オフライン優先、インストール不要のポータブル仕様。

![プラットフォーム](https://img.shields.io/badge/platform-Windows%2010%2B-0078D6)
![ランタイム](https://img.shields.io/badge/.NET-8.0-512BD4)
![ライセンス](https://img.shields.io/badge/license-MIT-green)

OMS は [osu!lazer](https://github.com/ppy/osu) をベースに、osu!・Taiko・Catch を取り除き、**BMS** と **osu!mania** を一つのよりモダンなクライアントにまとめたものです。オフライン優先・ポータブルで、ローカル譜面を直接読み込んでインポートできます。判定・スコア・ゲージ・スピードの仕様は IIDX / LR2 / beatoraja に合わせてあるため、これらのプラットフォームに慣れたプレイヤーならすぐに馴染めます。

## 目次

- [特徴](#特徴)
- [動作環境](#動作環境)
- [インストール](#インストール)
- [使い方](#使い方)
  - [オフライン優先](#オフライン優先)
  - [BGA 再生](#bga-再生)
- [ソースからのビルド](#ソースからのビルド)
- [ドキュメント](#ドキュメント)
- [プロジェクトの状況](#プロジェクトの状況)
- [コントリビュート](#コントリビュート)
- [ライセンス](#ライセンス)
- [謝辞](#謝辞)

## 特徴

- **2 つのモード** —— osu!mania と BMS。5 / 7 / 9 / 14K に対応。
- **判定とスコア** —— 4 種類の判定システム、EX / DJ スコアとバックライト（ランプ）フィードバック。
- **複数のゲージ** —— ASSIST EASY / EASY / NORMAL / HARD / EX-HARD / HAZARD / GAS。OMS LEGACY、beatoraja、LR2、IIDX のルールファミリーを切り替えられ、慣れ親しんだプラットフォームに近い CLEAR の感覚で遊べます。
- **BGA 再生** —— 静止背景、画像・動画 BGA、POOR レイヤーをレイアウトに応じて端に寄せたフローティングパネルで表示。古い動画形式も ffmpeg があれば再生できます（[使い方](#bga-再生)を参照）。
- **練習・アシスト Mod** —— Mirror / Random（R-RANDOM / S-RANDOM とカスタムパターンを含む）、Auto Scratch / Auto Note など練習向けの Mod。
- **幅広い入力対応** —— キーボード、XInput ゲームパッド、Raw Input、HID / DirectInput コントローラー。
- **BMS 難易度表** —— ローカルディレクトリと公開 URL ソースからのインポート、MD5 マッチング、表ごとのグループ表示。
- **ポータブル配布** —— インストール不要のフルパッケージ。データのルートディレクトリは移動可能。

## 動作環境

- Windows 10 22H2 以降
- .NET 8 / DesktopGL / osu-framework をベースに構築

## インストール

[GitHub Releases](https://github.com/ZDaMexy/oms/releases) から最新のポータブルフルパッケージ `oms_YYYYMMDD.zip` をダウンロードし、展開してそのまま実行してください。インストールは不要です。

更新する際は新しいパッケージをダウンロードして古いディレクトリを上書きし、`portable.ini`、（ポータブルモードの）`data/` フォルダ、カスタムデータルートで使われる `storage.ini` を残してください。ゲーム内のオンライン自動更新は既定で無効です。

## 使い方

譜面はファイルシステムから直接読み込まれます。BMS 譜面は `chartbms/`、mania 譜面は `chartmania/` に置くだけで、`.osz` への変換は不要です。Settings → Maintenance から複数の外部 / 内部ライブラリのルートを登録してスキャン・インポートすることもできます。

### オフライン優先

OMS は現在、完全にオフラインで動作します。アカウント、オンラインランキング、譜面ダウンロード、ニュース / チャット、マルチプレイや観戦などのオンライン機能は既定で非表示または無効になっており、今後の段階で順次開放する予定です。

唯一の例外は **BMS 難易度表** です。ローカルパスと公開 URL からのインポート / 更新に対応しており、OMS 独自のサーバーには一切依存しません。

### BGA 再生

BMS プレイ中、BGA はプレイフィールド横のフローティングパネルにレイアウトに応じて端に寄せて表示されます（1P は右、2P は左、中央は右、14K は中央）。静止背景、画像 BGA、POOR レイヤー、`.mp4` 動画はそのまま利用でき、全画面背景には譜面背景をぼかしたものが表示されます。BMS 設定の「BGA を表示」でパネル全体をオフにできます。

古い動画形式（`.mpg`、`.wmv`、`.avi`、`.flv`）は内蔵プレイヤーでデコードできず、既定では静止画像が表示されます。これらを再生するには ffmpeg を用意してください。

- システムの PATH に導入する：`winget install ffmpeg`（OMS が起動中なら一度再起動）、または
- [ffmpeg](https://www.gyan.dev/ffmpeg/builds/) をダウンロードし、`bin\ffmpeg.exe` を OMS のプログラムディレクトリ（`osu!.exe` の隣）またはデータディレクトリ（既定は `%APPDATA%\oms`）に置く。

その上で BMS 設定の「デコードできない BGA 動画をトランスコードする」を有効のままにしてください。該当する譜面を初めて開くとバックグラウンドでトランスコードが行われ、完了するまでは静止画像を表示し、完了後に動画へ切り替わります。結果はデータディレクトリの `bga-video-cache\` にキャッシュされ、次回以降はそのまま再生されます。

## ソースからのビルド

[.NET 8 SDK](https://dotnet.microsoft.com/download) と、Visual Studio・JetBrains Rider・Visual Studio Code のいずれかが必要です。`osu.Desktop.slnf` を開くことを推奨します。

```shell
# クローン
git clone https://github.com/ZDaMexy/oms.git
cd oms

# ビルド
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m

# 実行
dotnet run --project osu.Desktop

# BMS テストの実行
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore
```

## ドキュメント

製品の境界、開発計画、現状、技術的制約はすべて [`doc_md/`](doc_md/README.md) にまとめられています。

- [製品制約とリリースゲート](doc_md/mainline/OMS_COPILOT.md)
- [開発計画](doc_md/mainline/DEVELOPMENT_PLAN.md)
- [現状と未解決の課題](doc_md/mainline/DEVELOPMENT_STATUS.md)
- [変更履歴](doc_md/mainline/CHANGELOG.md)

リポジトリの案内と「コードを変更したらドキュメントも更新する」という規律は [CLAUDE.md](CLAUDE.md) に記載されています。

## プロジェクトの状況

OMS は **Phase 1**（ローカルの BMS / mania コアフロー）の仕上げ段階にあり、現在はスキンシステムの専用作業と入力ハードウェアの受け入れを進めています。それまでオンライン関連の Phase 3 機能は凍結されたままです。最新の進捗は [DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md) を正とします。

## コントリビュート

[Issue](https://github.com/ZDaMexy/oms/issues) でのフィードバックや Pull Request を歓迎します。コードを提出する前に、以下にご注意ください。

- `osu.Desktop.slnf` でのビルドを推奨し、Release ビルドが警告・エラーゼロであることを確認してください。
- BMS 関連のロジックを変更する場合は `osu.Game.Rulesets.Bms.Tests` を実行してください。
- 計画・状況・制約・検証結論を変える変更は、**同じコミット内で** [`doc_md/`](doc_md/README.md) の対応するガバナンスドキュメントを更新する必要があります（[CLAUDE.md](CLAUDE.md) を参照）。

## ライセンス

本プロジェクトは上流の osu!lazer から継承した [MIT ライセンス](LICENCE)の下で提供されます。

OMS は osu!lazer の方向性を定めたフォークであり、その目標と内容は上流から大きく分化しています。[`ppy/osu`](https://github.com/ppy/osu) のミラーや代替リリース元ではありません。

## 謝辞

- [osu!lazer](https://github.com/ppy/osu) と [osu-framework](https://github.com/ppy/osu-framework) —— OMS の上流の基盤。
- IIDX、LR2、beatoraja —— 判定・ゲージ・スピード仕様の方向性の参照元。
</content>
</invoke>
