# Contributing

[日本語](#貢献の方法日本語) | English

Bug reports, anti-cheat compatibility reports and pull requests are welcome. For building the app, see [Building](README.md#building) in the README. Please follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Translations

The user interface is available in English and Japanese. Translations into other languages are welcome.

### How the UI text is organized

- All UI strings are in `GlassMonitor/Resources/`:
  - `Strings.resx`: English. This is the default language. It is used for any string that is missing from a translation, and when Windows is in a language that GlassMonitor does not support.
  - `Strings.ja.resx`: Japanese.
- XAML uses `{l:Loc Key}` (for example `Text="{l:Loc Settings_Language}"`, with `xmlns:l="clr-namespace:GlassMonitor.Localization"`). C# uses `Localizer.Get("Key")`, or `Localizer.Format("Key", value)` for strings with placeholders such as `{0}`.
- The supported languages are listed in `GlassMonitor/Localization/Languages.cs`.
- With the default setting "Automatic", GlassMonitor uses the Windows display language if it is supported, and English otherwise. Users can choose a language in Settings → Language; the UI switches immediately.

### Adding a language

1. Copy `GlassMonitor/Resources/Strings.resx` to `Strings.<code>.resx` in the same folder. `<code>` is a .NET culture name such as `de`, `fr`, `zh-Hans` or `pt-BR`. Prefer a language-only code (`de` rather than `de-DE`): "Automatic" then also matches every region (`de-AT`, `de-CH`, …).
2. Translate each `<value>`:
   - Do not change the `name` attributes.
   - Keep placeholders such as `{0}`.
   - Each `<comment>` describes where the string is shown. You can delete the comments in your file.
   - Strings that you leave out are shown in English.
   - Keep short labels short. The settings panel is 280 pixels wide, and the status bar must fit in a window that is 900 pixels wide.
3. Add the language to `Supported` in `GlassMonitor/Localization/Languages.cs`, with its name written in that language. The order is the order in the settings:
   ```csharp
   new("de", "Deutsch"),
   ```
4. Add the code to `<SatelliteResourceLanguages>` in `GlassMonitor/GlassMonitor.csproj` (for example `en;ja;de`). This includes WPF's own text, such as the context menu of text boxes, in that language.
5. Check the translation:
   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\Check-Translations.ps1
   dotnet build GlassMonitor.slnx -c Release
   ```
   Then start `GlassMonitor\bin\Release\net10.0-windows\GlassMonitor.exe --lang de` and look at the tiles, the status bar, the settings panel and the dialogs (turning on "Start at sign-in" shows a confirmation, which you can cancel). `--lang` changes the language only for that run; your settings are not changed.
   To check the storage cards and the drive selection in the settings, which appear only with 2 or more and 5 or more disks, add `--demo-disks <2-5>`. It shows that many dummy disks instead of your real disks, and does not save any settings during that run.
6. Update the sentence about the available languages in the README (both the Japanese and the English sections).
7. (Optional) Take a screenshot as described below.
8. Open a pull request.

Nothing else needs to change. The new language appears in the settings, and release builds include `<code>/GlassMonitor.resources.dll` automatically.

### Adding or changing a string

1. Add the string to `Strings.resx` (English) with a key named `<Area>_<Name>`, for example `Settings_Language` or `Tile_Temperature`. The area is where the string is shown: `Window`, `Common`, `Settings`, `Tile`, `Temperature`, `Storage`, `Health`, `Status`, `AutoStart` or `PawnIo`. Add a `<comment>` if the context is not obvious.
2. Add the translation to every `Strings.<code>.resx`. If you cannot translate it, leave it out: English is shown, and the check script lists it as a warning.
3. Use the key from XAML (`{l:Loc Key}`) or C# (`Localizer.Get("Key")`).
4. Run `tools\Check-Translations.ps1`. It reports keys that are used in the code but missing from `Strings.resx`, missing or extra keys in each translation, and placeholders that do not match. A misspelled key is also visible in the app: the key itself is shown instead of the text.

## Screenshots

The README uses `docs/screenshot-ja.png` (Japanese section) and `docs/screenshot-en.png` (English section). GlassMonitor can take these screenshots itself:

```powershell
# In a terminal running as administrator, from the root of the repository
dotnet build GlassMonitor.slnx -c Release
Start-Process -Wait GlassMonitor\bin\Release\net10.0-windows\GlassMonitor.exe '--lang en --screenshot docs\screenshot-en.png'
Start-Process -Wait GlassMonitor\bin\Release\net10.0-windows\GlassMonitor.exe '--lang ja --screenshot docs\screenshot-ja.png'
```

- Exit GlassMonitor before you run these commands. Only one instance runs at a time; if GlassMonitor is already running, the command only brings it to the front.
- GlassMonitor starts with the default settings and the default size at the center of the screen, waits until the graphs have 60 seconds of data (about one minute), saves the window as a PNG file and exits. Your settings are not read or changed.
- The screenshot is taken from the screen, including the Acrylic background, so keep the window visible and do not move the mouse over it. The size depends on the display scaling (960 × 850 pixels at 100%).
- From a terminal that is not running as administrator, GlassMonitor asks for elevation and the working directory may change. Use an absolute path for `--screenshot` in that case.

---

## 貢献の方法（日本語）

不具合の報告、アンチチート互換性の報告、プルリクエストを歓迎します。ビルド方法は README の[ビルド](README.md#ビルド)を参照してください。[行動規範](CODE_OF_CONDUCT.md)を守ってください。

### 翻訳

画面は日本語と英語に対応しています。ほかの言語への翻訳も歓迎します。

#### 画面の文字列の構成

- 画面の文字列はすべて `GlassMonitor/Resources/` にあります。
  - `Strings.resx`：英語。既定の言語で、訳の無い文字列や、対応していない言語の Windows でもこれを使います。
  - `Strings.ja.resx`：日本語。
- XAML では `{l:Loc キー}`（例：`Text="{l:Loc Settings_Language}"`。`xmlns:l="clr-namespace:GlassMonitor.Localization"`）、C# では `Localizer.Get("キー")`、`{0}` などを含む文字列は `Localizer.Format("キー", 値)` を使います。
- 対応言語の一覧は `GlassMonitor/Localization/Languages.cs` にあります。
- 既定の設定「自動」では、Windows の表示言語が対応言語ならその言語、そうでなければ英語で表示します。利用者は「設定」→「言語」で選ぶこともでき、すぐに切り替わります。

#### 言語を追加する

1. `GlassMonitor/Resources/Strings.resx` を同じフォルダーに `Strings.<コード>.resx` としてコピーします。`<コード>` は .NET のカルチャ名（`de`、`fr`、`zh-Hans`、`pt-BR` など）です。地域を含まないコード（`de-DE` ではなく `de`）にすると、「自動」でどの地域（`de-AT`、`de-CH` など）にも当てはまります。
2. 各 `<value>` を翻訳します。
   - `name` 属性は変えないでください。
   - `{0}` などのプレースホルダーは残してください。
   - `<comment>` はその文字列が表示される場所の説明です。コピーしたファイルでは削除してかまいません。
   - 削除した（訳していない）文字列は英語で表示されます。
   - 短いラベルは短く訳してください。設定画面は幅 280 ピクセルで、ステータスバーは幅 900 ピクセルのウィンドウに収まる必要があります。
3. `GlassMonitor/Localization/Languages.cs` の `Supported` に、その言語で書いた言語名と一緒に追加します。並び順が設定画面での順番になります。
   ```csharp
   new("de", "Deutsch"),
   ```
4. `GlassMonitor/GlassMonitor.csproj` の `<SatelliteResourceLanguages>` にコードを追加します（例：`en;ja;de`）。WPF 自身の文字列（テキストボックスの右クリックメニューなど）もその言語で同梱されます。
5. 確認します。
   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\Check-Translations.ps1
   dotnet build GlassMonitor.slnx -c Release
   ```
   `GlassMonitor\bin\Release\net10.0-windows\GlassMonitor.exe --lang de` で起動し、タイル、ステータスバー、設定画面、ダイアログ（「サインイン時に自動起動」を ON にすると確認が出ます。キャンセルできます）を確認します。`--lang` はその起動だけ言語を切り替え、設定は変えません。
   ディスクが 2 台以上・5 台以上のときだけ表示されるストレージのカード表示や設定画面のドライブ選択は、`--demo-disks <2〜5>` を付けると確認できます。実際のディスクの代わりにその台数のダミーのディスクを表示し、その起動では設定を保存しません。
6. README の対応言語の記載（日本語と英語の両方）を更新します。
7. （任意）下の手順でスクリーンショットを撮ります。
8. プルリクエストを送ってください。

ほかに変更する箇所はありません。追加した言語は設定画面に表示され、リリースビルドには `<コード>/GlassMonitor.resources.dll` が自動で含まれます。

#### 文字列を追加・変更する

1. `Strings.resx`（英語）に `<場所>_<名前>` の形式のキー（例：`Settings_Language`、`Tile_Temperature`）で追加します。場所は表示される場所で、`Window`、`Common`、`Settings`、`Tile`、`Temperature`、`Storage`、`Health`、`Status`、`AutoStart`、`PawnIo` のどれかです。文脈がわかりにくいときは `<comment>` を付けてください。
2. すべての `Strings.<コード>.resx` に訳を追加します。訳せない場合は追加しなくてかまいません（英語で表示され、確認スクリプトが警告を出します）。
3. XAML（`{l:Loc キー}`）または C#（`Localizer.Get("キー")`）から使います。
4. `tools\Check-Translations.ps1` を実行します。コードで使っているのに `Strings.resx` に無いキー、各言語のキーの過不足、プレースホルダーの不一致を報告します。キーを書き間違えると、アプリでも文字列の代わりにキーがそのまま表示されます。

### スクリーンショット

README では `docs/screenshot-ja.png`（日本語）と `docs/screenshot-en.png`（英語）を使っています。GlassMonitor 自身で撮影できます。

```powershell
# 管理者として実行したターミナルで、リポジトリのルートから実行する
dotnet build GlassMonitor.slnx -c Release
Start-Process -Wait GlassMonitor\bin\Release\net10.0-windows\GlassMonitor.exe '--lang en --screenshot docs\screenshot-en.png'
Start-Process -Wait GlassMonitor\bin\Release\net10.0-windows\GlassMonitor.exe '--lang ja --screenshot docs\screenshot-ja.png'
```

- 実行する前に GlassMonitor を終了してください。同時に起動できるのは 1 つだけで、起動中のときは既存のウィンドウが前面に出るだけになります。
- 既定の設定・既定のサイズで画面中央に起動し、グラフが 60 秒分たまるのを待って（約 1 分）、ウィンドウを PNG で保存して終了します。利用者の設定は読み込まず、変更もしません。
- 画面に表示された状態（Acrylic の背景を含む）を撮るため、ウィンドウを隠したり、マウスをウィンドウの上に置いたりしないでください。画像の大きさは画面の拡大率で変わります（100% で 960 × 850 ピクセル）。
- 管理者ではないターミナルから実行すると昇格の確認が出て、作業フォルダーが変わることがあります。その場合は `--screenshot` に絶対パスを指定してください。
