# Code signing policy

[日本語](#コード署名ポリシー日本語) | English

Current releases of GlassMonitor are not code signed. This document describes the policy that applies if releases are code signed in the future (what gets signed, team roles), and the privacy policy.

## Team roles

GlassMonitor is maintained by a single person, who holds all roles.

- Committers and reviewers: [taki3100](https://github.com/taki3100)
- Approvers: [taki3100](https://github.com/taki3100)

| Role | Responsibility |
|---|---|
| Committers | Trusted to change the source code of this repository without additional review. |
| Reviewers | Review every change from outside contributors (pull requests) before it is merged. |
| Approvers | Approve each signing request in SignPath before a release is signed. |

All team members use multi-factor authentication (MFA) for GitHub and SignPath.

## What gets signed

- Only files built from the source code in this repository are signed: `GlassMonitor.exe`, `GlassMonitor.dll` and the UI language resources (`<language>/GlassMonitor.resources.dll`, e.g. `ja/GlassMonitor.resources.dll`).
- Release builds are produced by GitHub Actions on GitHub-hosted runners from a version tag (see [`.github/workflows/release.yml`](.github/workflows/release.yml)). Build scripts and CI configuration are part of this repository.
- Every signing request is approved manually by an approver.
- Third-party components (LibreHardwareMonitorLib and its dependencies, the .NET runtime) are shipped unchanged and are **not** signed with this project's certificate. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- Signed files carry the product name `GlassMonitor` and the release version.
- Releases published before code signing is introduced are unsigned.

## Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

In detail:

- GlassMonitor does not collect, store or send telemetry, usage data, crash reports or hardware information. It has no update check and makes no network connections.
- Sensor values are read locally and shown only on screen. Settings are stored locally in `%APPDATA%\GlassMonitor\settings.json`; unexpected errors are written to `%LOCALAPPDATA%\GlassMonitor\error.log` and never leave the computer.
- The only network-related action is opening a web page in your browser (for example the PawnIO download page) when you click the corresponding button.
- The included third-party components (LibreHardwareMonitorLib and its dependencies) do not communicate over the network as used by GlassMonitor. The PawnIO driver, which you install separately, is a local kernel driver and is governed by its own terms.

## System changes

GlassMonitor does not change system settings on its own. The only exception is the optional **start at sign-in** feature: when you turn it on, a confirmation dialog explains the change before a scheduled task named `GlassMonitor` is created in Task Scheduler. Turning the option off removes the task. Uninstallation steps are described in the [README](README.md#uninstall).

---

## コード署名ポリシー（日本語）

現在の GlassMonitor のリリースはコード署名されていません。このページは、将来リリースにコード署名する場合の方針（署名の対象・役割）と、プライバシーポリシーをまとめたものです。

### 役割

GlassMonitor は 1 人で開発しており、すべての役割を [taki3100](https://github.com/taki3100) が担います。

- コミッター・レビュアー: [taki3100](https://github.com/taki3100)
- 承認者: [taki3100](https://github.com/taki3100)

| 役割 | 内容 |
|---|---|
| コミッター | 追加のレビューなしでソースコードを変更できる |
| レビュアー | 外部からの変更（プルリクエスト）をマージ前にすべて確認する |
| 承認者 | リリースを署名する前に、SignPath で署名リクエストを 1 件ずつ承認する |

すべてのメンバーは GitHub と SignPath で多要素認証（MFA）を使用します。

### 署名の対象

- このリポジトリのソースからビルドした `GlassMonitor.exe`、`GlassMonitor.dll`、画面の言語リソース（`ja/GlassMonitor.resources.dll` など `<言語>/GlassMonitor.resources.dll`）だけを署名します。
- リリース用のビルドは、バージョンタグをもとに GitHub Actions（GitHub ホストのランナー）で作成します（[`.github/workflows/release.yml`](.github/workflows/release.yml)）。
- 署名リクエストは承認者が毎回手動で承認します。
- サードパーティのコンポーネント（LibreHardwareMonitorLib とその依存ライブラリ、.NET ランタイム）は変更せずに同梱し、このプロジェクトの証明書では署名しません。
- 署名するファイルの製品名は `GlassMonitor`、バージョンはリリースのバージョンです。
- コード署名を導入する前のリリースは署名されていません。

### プライバシーポリシー

このプログラムは、ユーザーまたはインストール・操作する人が明示的に求めた場合を除き、ネットワーク上の他のシステムに情報を送信しません。

- テレメトリ、利用状況、クラッシュレポート、ハードウェア情報を収集・送信しません。更新確認も行わず、ネットワークに接続しません。
- センサーの値はローカルで読み取って画面に表示するだけです。設定は `%APPDATA%\GlassMonitor\settings.json`、予期しないエラーは `%LOCALAPPDATA%\GlassMonitor\error.log` にローカル保存し、外部には送りません。
- ネットワークに関わる動作は、ボタンを押したときにブラウザーで Web ページ（PawnIO のダウンロードページなど）を開くことだけです。

### システムの変更

GlassMonitor が自動でシステム設定を変えることはありません。例外は任意の「サインイン時に自動起動」で、ON にすると確認ダイアログで内容を説明したうえで、タスク スケジューラに `GlassMonitor` タスクを登録します。OFF にするとタスクを削除します。アンインストール手順は [README](README.md#アンインストール) を参照してください。
