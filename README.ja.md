# OMS

[简体中文](README.md) | [English](README.en.md) | **日本語**

> BMS と osu!mania のための Windows 向け音楽ゲームクライアント。オフライン優先、インストール不要のポータブル仕様。

[![公式サイト](https://img.shields.io/badge/website-oms.zdamexy.work-FF6B35)](https://oms.zdamexy.work/)
![プラットフォーム](https://img.shields.io/badge/platform-Windows%2010%2B-0078D6)
![ランタイム](https://img.shields.io/badge/.NET-8.0-512BD4)
![ライセンス](https://img.shields.io/badge/license-MIT-green)

OMS は [osu!lazer](https://github.com/ppy/osu) をベースに、osu!・Taiko・Catch を取り除き、**BMS** と **osu!mania** を一つのよりモダンなクライアントにまとめたものです。オフライン優先・ポータブルで、ローカル譜面を直接読み込んでインポートできます。判定・スコア・ゲージ・スピードの仕様は IIDX / LR2 / beatoraja に合わせてあるため、これらのプラットフォームに慣れたプレイヤーならすぐに馴染めます。詳しくは公式サイト [oms.zdamexy.work](https://oms.zdamexy.work/) をご覧ください。

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
- **BGA 再生** —— 静止背景、画像・動画 BGA、POOR レイヤーに対応。ウィンドウの大きさと位置はスキンで設定できます。古い動画形式も ffmpeg があれば再生できます（[使い方](#bga-再生)を参照）。
- **練習・アシスト Mod** —— Mirror / Random（R-RANDOM / S-RANDOM とカスタムパターンを含む）、Auto Scratch / Auto Note など練習向けの Mod。
- **入力連携** —— キーボード、XInput、Raw Input、DirectInput/HID のソフトウェア経路を実装済み。実機対応範囲、アナログ皿、キャリブレーションの検証は未完了です。
- **BMS 難易度表** —— ローカルディレクトリと公開 URL ソースからのインポート、MD5 マッチング、表ごとのグループ表示。
- **ポータブル配布** —— インストール不要のフルパッケージ。データのルートディレクトリは移動可能。

## 動作環境

- Windows 10 22H2 以降
- .NET 8 / DesktopGL / osu-framework をベースに構築

## インストール

[GitHub Releases](https://github.com/ZDaMexy/oms/releases) から最新のポータブルフルパッケージ `oms_YYYYMMDD.zip` をダウンロードし、展開してそのまま実行してください。インストールは不要です。

更新時はゲームを終了し、新しいフルパッケージを別のディレクトリに展開して、その中の `Update-OMS.ps1` を実行し、既存のインストール先を指定してください。更新ツールは従来のポータブル／非ポータブル設定、ユーザーデータ、カスタム保存先を維持し、置き換える前のプログラムファイルも保存します。詳しくは[配布ガイド](doc_md/other/RELEASE.md)を参照してください。ゲーム内のオンライン自動更新は既定で無効です。

## 使い方

譜面はファイルシステムから直接読み込まれます。BMS 譜面は `chartbms/`、mania 譜面は `chartmania/` に置くだけで、`.osz` への変換は不要です。Settings → Maintenance から複数の外部 / 内部ライブラリのルートを登録してスキャン・インポートすることもできます。

### オフライン優先

OMS のゲームプレイ、ライブラリ、ユーザーデータ経路は既定でオフライン動作します。Phase 3 までは OMS 独自サービスを無効のままにし、既定 endpoint も空のままです。アカウント、オンラインランキング、OMS / osu!mania 公式ダウンロード、ニュース / チャット、マルチプレイや観戦などの機能は既定で非表示または無効です。

ゲーム内 Browse は、許可を得た公開 BMS 配信元 [Ginger Rush](https://gingerrush.com/) と [616 / Alvorna](https://616.sb/bms/download) に対応します。配信元→難易度表→表内レベルを選び、キーワードでも絞り込めます。コンパクトな楽曲カードを展開して譜面を選び、ダウンロードアイコンでパッケージを取得し、ライブラリ追加後に選曲画面で開けます。アイコンにカーソルを合わせると操作名が表示されます。難易度表が利用できない場合は案内が残り、検索アイコンや Enter で再試行して表とレベルの選択を復旧できます。画面を閉じてもダウンロードは継続し、失敗時は手動で再試行できます。楽曲試聴は追加後のローカル選曲機能を使用します。Browse を開くまで配信元に接続せず、OMS アカウントは不要です。パッケージがない場合、破損している場合や未対応の譜面は追加成功と表示しません。[ダウンロード契約](doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载)を参照してください。

同じ画面で osu!mania に切り替えると、[Sayobot ミラー](https://osu.sayobot.cn/)から曲名・作者・元の譜面セットIDで検索し、鍵数・星数範囲・収録状態で絞り込めます。カードを展開して難易度を選ぶと、動画なしの元パッケージが `chartmania` に追加され、完了アイコンや通知からその難易度を開けます。混在パッケージはmaniaのみ登録します。切り替えや画面を閉じてもダウンロードは継続し、mania画面を開くまでミラーには接続しません。公式アカウントは不要です。星数はミラーの資料、試聴はローカル選曲を使用します。公式ダウンロード、独立したオンライン試聴、再開機能は含みません。[maniaダウンロード契約](doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#sayobot-mania-浏览下载)を参照してください。

直近の記録済み実サイト受け入れ検証（2026-10-01）では、パッケージ取得時の安全な接続に失敗しました。実際のミラーパッケージの追加成功は引き続き未検証で、ソフトウェアとデスクトップの試験素材による経路には成功記録があります。これは当時の結果であり、接続先の継続的な障害を示すものではありません。[現在の P1-A 状態](doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)と[当時の検証記録](doc_md/other/MANIA_SAYOBOT_DOWNLOAD_20261001.md)を参照してください。

**BMS 難易度表** もローカルパスと公開 URL からのインポート / 更新に対応しており、OMS 独自のサーバーには一切依存しません。

### BGA 再生

BMS プレイ中、スキンで最大 16 個の BGA ウィンドウの位置・大きさと、全体表示／中央切り抜き／引き伸ばしを設定できます。ウィンドウを無効にしても、独立した情報欄は残せます。すべてのウィンドウは同じ譜面演出を共有し、表示レイアウトを作り直しても再生位置は維持されます。ウィンドウ指定のない従来のスキンでは、通常 1P が右、2P が左、中央配置が右、14K が四隅という自動配置を維持し、スペース不足時には配置とプレイフィールドを調整します。明示指定したウィンドウがレーンや HUD などの保護領域に重なる場合は、勝手に移動せずグループ全体を非表示にします。設定方法と例は[スキン制作マニュアル](skin-authoring/docs/SKINNING.md)（中国語）を参照してください。

静止背景、画像 BGA、POOR レイヤー、`.mp4` 動画はそのまま利用でき、全画面背景には譜面背景をぼかしたものが表示されます。BMS 設定の「BGA を表示」でウィンドウをオフにできます。BGA は現在ネイティブ BMS のみが対象で、BMS から mania に変換した譜面では利用できません。

古い動画形式（`.mpg`、`.wmv`、`.avi`、`.flv`）は内蔵プレイヤーでデコードできず、既定では静止画像が表示されます。これらを再生するには ffmpeg を用意してください。

- システムの PATH に導入する：`winget install ffmpeg`（OMS が起動中なら一度再起動）、または
- [ffmpeg](https://www.gyan.dev/ffmpeg/builds/) をダウンロードし、`bin\ffmpeg.exe` を OMS のプログラムディレクトリ（`osu!.exe` の隣）またはデータディレクトリ（既定は `%APPDATA%\oms`）に置く。

その上で BMS 設定の「ffmpeg完整BGA支持」（ffmpeg による BGA 対応）を有効のままにしてください。初回はロード中に最長約 8 秒待機し、間に合えば動画を先頭から再生します。タイムアウトした場合は静止画像を表示し、完了後に動画へ切り替わります。`bga-video-cache\` は現在のプロセスセッション内だけで再利用され、OMS の再起動時に消去された後、必要に応じて再トランスコードされます。

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

リポジトリの案内と「コードを変更したらドキュメントも更新する」という規律は [AGENTS.md](AGENTS.md) に記載されています。`CLAUDE.md` は互換用の案内に限られます。

## プロジェクトの状況

OMS は **Phase 1.x**（ローカルの BMS / mania とスキン）の仕上げ段階です。「静线」（oms-simple）が両モード唯一の内蔵・既定・フォールバックスキンです。通常の開発起動、ビルド、発行時に作者ソースの更新が取り込まれ、プレイヤーによるインポートは不要です。「星轨」（oms-complex）は内蔵対象から外れ、リポジトリ内の旧ファイルは過去の参考資料として残っています。作者は[制作キット](skin-authoring/README.md)からテンプレートを編集・検証・梱包し、自作スキンをインポートできます。設定では BMS と mania のスキンを個別に保存し、固定の `chartskin` フォルダーを開いて手動で再読み込みできます。復元されたコンポーネント配置エディターでは、配置・プロパティの変更や画像のインポートを行い、独立したコピーとして保存できます。静线のレイアウト・情報欄・レーン比率の調整は反映済みで、見た目の改善はユーザーの判断で一時停止中です。全体の見た目・実機入力・長時間利用の受け入れは未完了で、Skin V1 と全体のリリースも完了していません。Phase 3 のオンライン機能は凍結したままです。最新の進捗と受け入れ状況は [DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md) を正とします。

## コントリビュート

[Issue](https://github.com/ZDaMexy/oms/issues) でのフィードバックや Pull Request を歓迎します。コードを提出する前に、以下にご注意ください。

- `osu.Desktop.slnf` でのビルドを推奨します。Release はエラーゼロかつ未説明の警告を増やさないことが必要です。既知の警告基準は [DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md) を参照してください。
- BMS 関連のロジックを変更する場合は `osu.Game.Rulesets.Bms.Tests` を実行してください。
- 計画・状況・制約・検証結論を変える変更は、**同じコミット内で** [`doc_md/`](doc_md/README.md) の対応するガバナンスドキュメントを更新し、`powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1` と `git diff --check` を実行してください（[AGENTS.md](AGENTS.md) を参照）。

## ライセンス

本プロジェクトは上流の osu!lazer から継承した [MIT ライセンス](LICENCE)の下で提供されます。

OMS は osu!lazer の方向性を定めたフォークであり、その目標と内容は上流から大きく分化しています。[`ppy/osu`](https://github.com/ppy/osu) のミラーや代替リリース元ではありません。

## 謝辞

- [osu!lazer](https://github.com/ppy/osu) と [osu-framework](https://github.com/ppy/osu-framework) —— OMS の上流の基盤。
- IIDX、LR2、beatoraja —— 判定・ゲージ・スピード仕様の方向性の参照元。
