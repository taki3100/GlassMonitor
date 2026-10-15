# Third-party notices / サードパーティ ライセンス

GlassMonitor itself is licensed under the [MIT License](LICENSE).
The release package also contains the following third-party components. They are included **unmodified**, in binary form, as distributed on NuGet. The full license texts are in the [`licenses`](licenses) folder (also included in the release zip).

GlassMonitor 自体は [MIT ライセンス](LICENSE) です。配布パッケージには、以下のサードパーティ製コンポーネントを NuGet で配布されているバイナリのまま（**改変せずに**）同梱しています。ライセンス全文は [`licenses`](licenses) フォルダー（リリースの zip にも同梱）にあります。

| Component | Version | License | Files | Source |
|---|---|---|---|---|
| LibreHardwareMonitorLib | 0.9.6 | MPL-2.0 | `LibreHardwareMonitorLib.dll` | https://github.com/LibreHardwareMonitor/LibreHardwareMonitor |
| PawnIO modules (embedded in LibreHardwareMonitorLib) | as shipped in LibreHardwareMonitorLib 0.9.6 | LGPL-2.1 | resources inside `LibreHardwareMonitorLib.dll` | https://github.com/namazso/PawnIO.Modules |
| DiskInfoToolkit | 1.1.2 | MPL-2.0 | `DiskInfoToolkit.dll` | https://github.com/Blacktempel/DiskInfoToolkit |
| RAMSPDToolkit-NDD | 1.4.2 | MPL-2.0 | `RAMSPDToolkit-NDD.dll` | https://github.com/Blacktempel/RAMSPDToolkit |
| BlackSharp.Core | 1.0.4 | MPL-2.0 | `BlackSharp.Core.dll` | https://github.com/Blacktempel/BlackSharp |
| HidSharp | 2.6.4 | Apache-2.0 | `HidSharp.dll` | https://software.seekye.com/hidsharp |
| Mono.Posix.NETStandard | 1.0.0 | MIT | `Mono.Posix.NETStandard.dll`, `MonoPosixHelper.dll`, `libMonoPosixHelper.dll` | https://github.com/mono/mono |
| .NET Runtime, Windows Desktop Runtime (WPF), System.Management, System.IO.Ports and other `System.*` / `Microsoft.*` libraries | 10.0 | MIT | runtime files in the application folder | https://github.com/dotnet/runtime, https://github.com/dotnet/wpf |

## Notes / 補足

- **MPL-2.0** (LibreHardwareMonitorLib, DiskInfoToolkit, RAMSPDToolkit-NDD, BlackSharp.Core): The source code of these components is available at the repositories listed above. GlassMonitor uses them unmodified.
  これらのソースコードは上記のリポジトリで公開されています。GlassMonitor は改変せずに使用しています。
- **LGPL-2.1** (PawnIO modules): LibreHardwareMonitorLib contains compiled PawnIO modules from the PawnIO.Modules project as embedded resources. Their source code is available at https://github.com/namazso/PawnIO.Modules.
  LibreHardwareMonitorLib には PawnIO.Modules のコンパイル済みモジュールが埋め込まれています。ソースコードは上記で公開されています。
- **PawnIO driver**: The PawnIO kernel driver itself (GPL-2.0, https://github.com/namazso/PawnIO) is **not** included. Users install it separately from https://pawnio.eu/.
  PawnIO ドライバー本体は同梱していません。ユーザーが別途インストールします。
- **.NET runtime**: The self-contained build includes the .NET and Windows Desktop runtimes, which are licensed under the MIT License. The runtime also ships native components from Microsoft (for example `vcruntime140_cor3.dll` and `D3DCompiler_47_cor3.dll`); see the third-party notices of [dotnet/runtime](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT) and [dotnet/wpf](https://github.com/dotnet/wpf/blob/main/THIRD-PARTY-NOTICES.TXT).
  self-contained ビルドには .NET と Windows Desktop のランタイム（MIT ライセンス）が含まれます。

## License texts / ライセンス全文

| File | License |
|---|---|
| [licenses/MPL-2.0.txt](licenses/MPL-2.0.txt) | Mozilla Public License 2.0 |
| [licenses/LGPL-2.1.txt](licenses/LGPL-2.1.txt) | GNU Lesser General Public License v2.1 |
| [licenses/Apache-2.0-HidSharp.txt](licenses/Apache-2.0-HidSharp.txt) | Apache License 2.0 (HidSharp, Copyright 2010-2025 James F. Bellinger) |
| [licenses/MIT-Mono.Posix.txt](licenses/MIT-Mono.Posix.txt) | MIT License (Mono.Posix.NETStandard) |
| [licenses/MIT-dotnet-runtime.txt](licenses/MIT-dotnet-runtime.txt) | MIT License (.NET Runtime, Copyright (c) .NET Foundation and Contributors) |
| [licenses/MIT-dotnet-wpf.txt](licenses/MIT-dotnet-wpf.txt) | MIT License (WPF, Copyright (c) .NET Foundation and Contributors) |
