# アンチチート互換性の点検結果

GlassMonitor が、ゲームのアンチチート（Easy Anti-Cheat、BattlEye、Riot Vanguard、FACEIT など）に検知・ブロックされる要素を含んでいないかを点検した記録です。

| 項目 | 内容 |
|---|---|
| 点検日 | 2026-10-15 |
| 対象 | GlassMonitor 0.1.1（`GlassMonitor/` のソースと、win-x64 self-contained の Release ビルド出力 266 ファイル。点検は 0.1.0 のコードで実施し、0.1.1 での変更は説明文の表記のみ） |
| 依存ライブラリ | LibreHardwareMonitorLib 0.9.6（ソースはコミット `3d331e3` を確認）、DiskInfoToolkit 1.1.2、RAMSPDToolkit-NDD 1.4.2、BlackSharp.Core 1.0.4、HidSharp 2.6.4 |
| 点検した環境 | Windows 11 Pro（10.0.26300）、Intel Core Ultra 7 265K、NVIDIA GeForce RTX 5070 Ti、PawnIO 2.2.0 |

## 点検の方法と範囲

- すべて**読み取りのみ**で調べました。ゲームやアンチチートのプロセス・ドライバー・ファイルには触れていません（ハンドルを開かない、内容を読まない、停止しない）。アンチチートについては、レジストリにサービスの登録があるかどうかだけを確認しました。
- ゲームは起動していません。アンチチートとの組み合わせでの実際の動作は**未検証**です。
- 検知の回避策は調査も実装もしていません。
- 判定の意味
  - **問題なし**：アンチチートに検知・ブロックされる要素が見当たらない
  - **要注意**：アンチチートが監視・評価する可能性のある要素がある（直ちに問題とは限らない）
  - **リスクあり**：ブロックされることが分かっている、またはその可能性が高い

## まとめ

| # | 項目 | 判定 | 理由 |
|---|---|---|---|
| 1 | GlassMonitor 自体のネイティブ呼び出し | 問題なし | 自分のウィンドウの操作、システム情報の取得、タスク スケジューラの登録だけ。他プロセスやドライバーには触れない |
| 2 | 他プロセスへのアクセス | 問題なし | OpenProcess、メモリの読み書き、DLL 注入、フック、他プロセスへの描画はどれも無い（自作コード・LHM のソース・同梱バイナリで確認） |
| 3 | 最前面表示 | 問題なし | WPF の `Window.Topmost` だけで実現。他のウィンドウの Z 順は変えず、ゲーム画面へのオーバーレイ描画もしない |
| 4 | LibreHardwareMonitorLib の有効な機能 | 要注意 | CPU とマザーボードのセンサーは、カーネルドライバー PawnIO を経由して読む |
| 5 | カーネルドライバー PawnIO の利用（FACEIT） | リスクあり | FACEIT は PawnIO をブロックする（既知の非対応）。GlassMonitor は CPU センサー制限モードで動き続ける |
| 6 | カーネルドライバー PawnIO の利用（その他のアンチチート） | 要注意 | 動作は未検証。ドライバーの読み込みはアンチチートが監視する対象になり得る |
| 7 | LHM による実行可能メモリの確保 | 要注意 | CPUID 命令を実行するため、自プロセス内に読み書き実行可能（RWX）なメモリを確保する。他プロセスには及ばない |
| 8 | WinRing0 | 問題なし | ドライバーファイルは同梱されていない。文字列は RAMSPDToolkit の未使用のインターフェース名だけ |
| 9 | SMBus へのアクセス（メモリ温度） | 問題なし | 既定は OFF で、OFF の間は SMBus に一切アクセスしない。ON にした場合は要注意 |
| 10 | USB HID デバイスへのアクセス | 問題なし | HidSharp.dll は同梱されているが、使う機能（Controller・PSU）は無効 |
| 11 | PawnIO.sys の署名 | 問題なし | 署名は有効（Microsoft Windows Hardware Compatibility Publisher） |
| 12 | コア分離（メモリ整合性）との両立 | 問題なし | メモリ整合性と脆弱なドライバーのブロックリストが有効な状態で、PawnIO が読み込まれて動作している |
| 13 | ネットワーク通信 | 問題なし | 通信しない（PawnIO の案内でブラウザーを開くときだけ、ユーザー操作で外部サイトを開く） |
| 14 | 自動起動（タスク スケジューラ） | 問題なし | 自分の exe を起動するログオン タスクだけ。ユーザーの確認後に登録する |

## 1. GlassMonitor 自体のネイティブ呼び出し — 問題なし

`GlassMonitor/` のソースにある P/Invoke・COM・WMI と、ネイティブ呼び出しを伴う主な .NET API の一覧です。

| DLL / API | 関数 | ファイル | 用途 |
|---|---|---|---|
| dwmapi.dll | `DwmSetWindowAttribute` | Interop/WindowBackdrop.cs | 自分のウィンドウのダークモード、角丸、枠線の色、Acrylic 背景 |
| dwmapi.dll | `DwmExtendFrameIntoClientArea` | Interop/WindowBackdrop.cs | Acrylic を自分のウィンドウ全体に広げる |
| user32.dll | `GetWindowPlacement` / `SetWindowPlacement` | Interop/WindowPlacement.cs | 自分のウィンドウの位置・サイズの保存と復元 |
| user32.dll | `GetWindowRect` / `MonitorFromWindow` / `GetMonitorInfo` / `SetWindowPos` | Interop/WindowPlacement.cs | 自分のウィンドウが画面外にはみ出したときに作業領域へ戻す（`SWP_NOZORDER` を指定し、Z 順は変えない） |
| user32.dll | `GetSystemMetricsForDpi` | MainWindow.xaml.cs | 最大化時の枠の太さを求める |
| user32.dll | `DefWindowProcW` | Interop/WindowBackdrop.cs | 自分のウィンドウの `WM_NCACTIVATE` を処理し、非アクティブ時も Acrylic を保つ |
| user32.dll | `IsIconic` / `ShowWindow` / `SetForegroundWindow` | App.xaml.cs | 2 つ目の GlassMonitor を起動したとき、起動済みの GlassMonitor のウィンドウを前面に出す |
| kernel32.dll | `GlobalMemoryStatusEx` | Services/PhysicalMemoryStatus.cs | メモリ使用量 |
| cfgmgr32.dll | `CM_Locate_DevNodeW` / `CM_Get_Parent` / `CM_Get_DevNode_PropertyW` | Services/PcieLink.cs | NVMe コントローラーの PCIe リンク速度を PnP のプロパティストアから読む（デバイスには問い合わせない） |
| COM | `Schedule.Service`（タスク スケジューラ） | Services/AutoStartTask.cs | 自動起動のタスクの登録・解除・確認（ユーザーが設定を ON/OFF したときだけ） |
| WMI | `Win32_PhysicalMemory` | Services/MemoryModules.cs | メモリの容量・規格・速度（起動時に 1 回） |
| .NET | `Process.GetProcessesByName`（自分と同じ名前）、`Process.MainWindowHandle` | App.xaml.cs | 二重起動の防止。プロセス一覧から自分と同じ名前のものを探すだけで、他のプロセスは開かない |
| .NET | `Process.Start("explorer.exe", url)` | ViewModels/DialogViewModels.cs | PawnIO の配布ページをブラウザーで開く（ボタンを押したときだけ） |
| .NET | `DriveInfo`、`Clipboard`、`Mutex`、`WindowsIdentity` | Services/DriveReader.cs ほか | ボリュームの容量とラベル、winget コマンドのコピー、二重起動の判定、管理者権限の判定 |

センサーの読み取りはすべて LibreHardwareMonitorLib が行います。LHM が使う主なネイティブ呼び出しは次のとおりです（LHM 0.9.6 の `NativeMethods.txt` と `Interop/` から確認）。

| 対象 | 呼び出し | 用途 |
|---|---|---|
| PawnIO | `CreateFile("\\?\GLOBALROOT\Device\PawnIO")`、`DeviceIoControl` | PawnIO にモジュールを読み込ませ、MSR（CPU の温度・電力・クロック）、LPC の Super I/O（ファン回転数）、SMBus（メモリ温度、ON のときのみ）を読む |
| ストレージ | `CreateFile("\\.\PhysicalDriveN")`、`DeviceIoControl`（DiskInfoToolkit） | SMART / NVMe の情報、ディスクの読み書き量 |
| GPU | `nvapi64.dll`・NVML（NVIDIA）、`D3DKMT*`、SetupAPI / `CM_*` | GPU のセンサー、VRAM 使用量 |
| CPU 使用率 | `NtQuerySystemInformation`（SYSTEM_PROCESSOR_PERFORMANCE_INFORMATION） | システム全体の CPU 使用率 |
| CPUID | `VirtualAlloc`（PAGE_EXECUTE_READWRITE）、`SetThreadGroupAffinity` | 自プロセス内で CPUID / RDTSC を実行し、自分のスレッドを各コアに割り当てて読む（7. を参照） |
| SMBIOS | `GetSystemFirmwareTable` | マザーボード名などの識別 |

## 2. 他プロセスへのアクセス — 問題なし

次の 3 か所を、他プロセスの操作に使う API 名で検索しました。

- 自作コード（`GlassMonitor/*.cs`）
- LibreHardwareMonitorLib 0.9.6 のソース全体
- ビルド出力のうち GlassMonitor とサードパーティのバイナリ（GlassMonitor.dll/.exe、LibreHardwareMonitorLib.dll、DiskInfoToolkit.dll、RAMSPDToolkit-NDD.dll、BlackSharp.Core.dll、HidSharp.dll、Mono.Posix 関連）。ASCII と UTF-16 の文字列として検索

検索した API：`OpenProcess`、`NtOpenProcess`、`ReadProcessMemory`、`WriteProcessMemory`、`NtReadVirtualMemory`、`NtWriteVirtualMemory`、`VirtualAllocEx`、`VirtualProtectEx`、`CreateRemoteThread`、`NtCreateThreadEx`、`QueueUserAPC`、`SetThreadContext`、`SetWindowsHookEx`、`CreateToolhelp32Snapshot`、`Process32First`、`EnumProcesses`、`MapViewOfFile`、`UpdateLayeredWindow`

結果、どれも見つかりませんでした。見つかったのは自プロセス内で完結する `LoadLibrary`（GPU のベンダーライブラリや .NET のホストの読み込み）、`DeviceIoControl`（PawnIO・ディスク）、`VirtualAlloc`（7. を参照）だけです。.NET ランタイム本体（`System.Diagnostics.Process` など）にはこれらの API 名が含まれますが、GlassMonitor のコードからは他プロセスを開く使い方をしていません。

- DLL 注入、API フック、他プロセスへのオーバーレイ描画：無し
- ゲームのプロセスやウィンドウを探す処理：無し（二重起動防止で探すのは「GlassMonitor」という名前のプロセスだけ）

## 3. 最前面表示 — 問題なし

- 「常に最前面」は `MainWindow.xaml` の `Topmost="{Binding Topmost}"`（WPF の `Window.Topmost`）だけで実現しています。
- `SetWindowPos` は画面外補正にだけ使い、`SWP_NOZORDER` を指定しているので Z 順は変えません。`HWND_TOPMOST` を直接指定する呼び出しはありません。
- 設定画面のポップアップは GlassMonitor 自身のウィンドウです。クリックを透過させるオーバーレイ（`WS_EX_TRANSPARENT` など）や、DirectX へのフックによるゲーム画面内への描画はしていません。
- 注意点：フルスクリーン（排他モード）のゲームの上には表示されません。ボーダーレスウィンドウのゲームでは、通常の最前面ウィンドウとして重なります。

## 4. LibreHardwareMonitorLib のバージョンと有効な機能 — 要注意

バージョン：**0.9.6**（NuGet の最新安定版）。`Services/SensorReader.cs` で `Computer` を次のように設定しています。

| フラグ | 値 | 読み取り方法 | 備考 |
|---|---|---|---|
| `IsCpuEnabled` | true | PawnIO（IntelMSR / AMDFamily / RyzenSMU モジュール） | カーネルドライバー経由 |
| `IsGpuEnabled` | true | NVAPI / NVML / ADL / IGCL、D3DKMT | ユーザーモードのベンダーライブラリ |
| `IsMotherboardEnabled` | true | PawnIO（LpcIO モジュール：Super I/O のポート I/O） | カーネルドライバー経由 |
| `IsStorageEnabled` | true | DiskInfoToolkit（物理ディスクへの IOCTL） | 管理者権限で動作 |
| `IsMemoryEnabled` | 設定次第（既定 false） | ON のとき PawnIO（SmbusI801 などのモジュール）で SPD を読む | 9. を参照 |
| `IsControllerEnabled` | false | — | USB HID の冷却機器は扱わない |
| `IsPsuEnabled` | false | — | USB HID の電源は扱わない |
| `IsNetworkEnabled` | false | — | — |
| `IsBatteryEnabled` | false | — | — |
| `IsPowerMonitorEnabled` | false | — | シリアル接続の電力計は扱わない |

CPU とマザーボードのセンサーは、カーネルドライバー PawnIO を経由して読みます。アンチチートはカーネルドライバーの読み込みを監視していることが多いため「要注意」としました（5.・6. を参照）。

## 5. PawnIO と FACEIT — リスクあり

- FACEIT のアンチチートは PawnIO をブロックします（PawnIO の署名証明書がチートソフトと共通のため、という報告に基づく既知の非対応）。この点検では FACEIT が入っていないため、実際の挙動は確認していません。
- PawnIO がブロックされた場合、GlassMonitor は終了せず **CPU センサー制限モード**で動作します。CPU の温度・クロック・消費電力とファン回転数は「—」になり、GPU・メモリ・ストレージの表示は続きます。
- 補足：この環境の `PawnIO.sys` の署名は Microsoft Windows Hardware Compatibility Publisher によるもので（11. を参照）、ファイルの署名からは FACEIT がブロックする理由は判別できません。

## 6. PawnIO とその他のアンチチート — 要注意

- Easy Anti-Cheat、BattlEye、Riot Vanguard などとの組み合わせは**未検証**です。
- PawnIO は、アプリが読み込ませた Pawn スクリプトのモジュールをドライバー内で実行する仕組みで、WinRing0 とは作りが異なります。ただし、アンチチートが PawnIO をどう扱うかはアンチチート側の判断によります。
- PawnIO のサービスは手動起動（Start=3）で、PawnIO を使うアプリが開いたときに読み込まれ、その後も読み込まれたままになります。そのため、GlassMonitor とゲームを起動した順番によって結果が変わる可能性があります。報告用の Issue テンプレートで起動順を記録できるようにしました。

## 7. LibreHardwareMonitorLib による実行可能メモリの確保 — 要注意

- LHM の `Hardware/OpCode.cs` は、CPUID と RDTSC の機械語を実行するため、`VirtualAlloc` で `PAGE_EXECUTE_READWRITE` のメモリを確保します。
- 確保するのは GlassMonitor 自身のプロセス内だけで、他のプロセス（ゲーム）には一切影響しません。
- 読み書き実行可能（RWX）なメモリは、プロセスを走査するタイプの検知で注目されることがあるため「要注意」としました。これは LHM の実装によるもので、GlassMonitor 側では変更していません。

## 8. WinRing0 — 問題なし

- ビルド出力（266 ファイル）に `.sys` / `.inf` / `.cat` ファイルはありません。ファイル名に WinRing / inpout / ring0 を含むものもありません。
- 「WinRing0」という文字列が見つかったのは `RAMSPDToolkit-NDD.dll` だけです。中身は `IWinRing0Driver` インターフェースと、列挙値 `DriverImplementation.WinRing0` です。
  - RAMSPDToolkit-NDD はドライバーを同梱しない版（No Driver Distribution）で、呼び出し側がドライバー実装を渡す仕組みです。LibreHardwareMonitor は PawnIO を使う独自の実装（`Custom`）を渡すため、WinRing0 の経路は使われません。
- LibreHardwareMonitorLib 0.9.6 のソースには WinRing0 への参照がありません。ローレベルのアクセスはすべて PawnIO 経由です。

## 9. SMBus へのアクセス（メモリ温度） — 問題なし（既定 OFF）

- メモリ温度は既定で OFF です。OFF の間は `IsMemoryEnabled = false` にしているため、SMBus に一切アクセスしません（LHM 0.9.6 は `IsMemoryEnabled = true` だと SMBus 上の DIMM を必ず走査します）。メモリ使用量は `GlobalMemoryStatusEx` から読みます。
- 設定で ON にすると、PawnIO の SMBus モジュール経由で SPD を読みます。この場合はカーネルドライバー経由のアクセスが増えるため「要注意」です。

## 10. USB HID デバイスへのアクセス — 問題なし

- `HidSharp.dll` は LibreHardwareMonitorLib の依存として同梱されていますが、HID を使う Controller（冷却機器）と PSU（電源）の機能を無効にしているため呼び出されません。
- Corsair iCUE SDK などのベンダー SDK は使っていません。

## 11. PawnIO のバージョンと署名 — 問題なし

| 項目 | 値 |
|---|---|
| インストール済みのバージョン | 2.2.0（アンインストール情報の DisplayVersion は 2.2.0.0） |
| ドライバーファイル | `C:\Windows\System32\DriverStore\FileRepository\pawnio.inf_amd64_a72a2f969b8b7496\PawnIO.sys`（ファイルバージョン 2.2.0） |
| サービスの状態 | RUNNING（Start=3：手動起動） |
| `Get-AuthenticodeSignature` | **Valid** |
| 署名者 | CN=Microsoft Windows Hardware Compatibility Publisher, O=Microsoft Corporation |
| 発行者 | CN=Microsoft Windows Third Party Component CA 2014 |
| 証明書の拇印 | 3847B761C2846DB72F61149176D09F9BB2D43E8A |
| 証明書の有効期限 | 2026-07-15（署名にタイムスタンプがあるため、期限後も署名は有効と判定される） |

## 12. コア分離（メモリ整合性）との両立 — 問題なし

| 項目 | 値 |
|---|---|
| メモリ整合性（HVCI）のレジストリ設定 | 有効（`HypervisorEnforcedCodeIntegrity\Enabled = 1`） |
| 実行中のセキュリティサービス（Win32_DeviceGuard） | `SecurityServicesRunning = 2`（HVCI が動作中）、VBS の状態 = 2（実行中） |
| 脆弱なドライバーのブロックリスト | 有効（`VulnerableDriverBlocklistEnable = 1`） |
| この状態での PawnIO | 読み込まれて動作している（GlassMonitor で CPU 温度・消費電力・ファン回転数を取得できた） |

メモリ整合性を無効にする必要はありません。

## 13. インストール済みのアンチチート（環境の確認） — 参考

レジストリの `HKLM\SYSTEM\CurrentControlSet\Services` にサービスの登録があるかだけを確認しました（サービスやドライバーの状態の問い合わせ、ファイルへのアクセスはしていません）。

| サービス名 | アンチチート | 登録 |
|---|---|---|
| EasyAntiCheat | Easy Anti-Cheat | なし |
| EasyAntiCheat_EOS | Easy Anti-Cheat（Epic Online Services） | **あり**（手動起動のサービス） |
| BEService / BEDaisy | BattlEye | なし |
| vgc / vgk | Riot Vanguard | なし |
| FACEIT / FACEITService | FACEIT AC | なし |
| ESEADriver2 | ESEA | なし |
| EAAntiCheatService | EA Javelin Anticheat | なし |
| PnkBstrA | PunkBuster | なし |
| npggsvc | nProtect GameGuard | なし |
| xhunter1 | XIGNCODE3 | **あり**（手動起動のドライバー） |
| mhyprot3 | HoYoverse | なし |
| ACE-BASE / AntiCheatExpert Service | Tencent ACE | **ACE-BASE のみあり**（手動起動のドライバー） |

アンチチートに対応したゲームを手元で起動していないため、これらとの実際の組み合わせは未検証です。

## 14. ネットワーク通信・自動起動 — 問題なし

- GlassMonitor はネットワーク通信をしません。テレメトリや更新確認もありません。
- 自動起動は、ユーザーが設定で ON にし、確認ダイアログで承認したときだけ、自分の exe を起動するログオン タスクをタスク スケジューラに登録します。OFF にすると削除します。

## 動作報告のお願い

アンチチートとの互換性は、実際のゲームでの報告をもとに更新します。GitHub の Issue の「アンチチート互換性の報告」テンプレートから、ゲーム名、アンチチート名、結果、GlassMonitor と PawnIO のバージョン、起動した順番を送ってください。GlassMonitor と PawnIO のバージョンは、設定画面のいちばん下に表示されます。
