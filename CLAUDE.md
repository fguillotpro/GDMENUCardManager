# CLAUDE.md — GDMENUCardManager

Dreamcast GDEMU/GDMenu/openMenu SD card manager. .NET solution in `src/`, two UIs over a shared core.

## Build

```
dotnet build src/GDMENUCardManager.sln -nologo -p:RestoreSources=https://api.nuget.org/v3/index.json
```

- Needs the **.NET 9 SDK**.
- Baseline: **0 errors, 39 pre-existing warnings** — don't chase them: CS0168 unused `ex` (Core/Manager.cs, WPF AboutWindow.xaml.cs), CS3021 CLSCompliant noise in SevenZip, SYSLIB0003/0051 obsolete CAS APIs, CS0108 `PropertyChanged` hiding in the Avalonia windows.

### Why .NET 9 SDK and why that `-p:RestoreSources` flag

- Avalonia 12.1.0's XAML source generator targets Roslyn 4.14. The .NET 8 SDK ships Roslyn 4.11 → generator is skipped (CS9057 analyzer warnings) and the build fails with CS0103 `InitializeComponent` / `dg1` / `DropLine` missing in `GDMENUCardManager.AvaloniaUI`. .NET 9 SDK fixes it.
- `src/GDMENUCardManager.AvaloniaUI/nuget.config` adds the dead-ish `myget.org/F/avalonia-ci` feed; NuGet hangs retrying it. `-p:RestoreSources=...nuget.org...` pins the source (repo files untouched). After the first successful restore the packages are cached, so plain `dotnet build` also works.

## Solution layout (`src/GDMENUCardManager.sln`)

|Project|TFM|Notes|
|---|---|---|
|`GDMENUCardManager`|net8.0-windows|WPF+WinForms classic UI (`WinExe`, AssemblyName `GDMENUCardManager`); ships `7z.dll`/`7z64.dll`; compiles the embedded SevenZip C# sources in its `SevenZip/` folder (hence the CS3021 warnings)|
|`GDMENUCardManager.AvaloniaUI`|net8.0|Avalonia 12.1.0 cross-platform UI; `AssemblyName` is also `GDMENUCardManager` — the two apps emit the same dll name in their own output dirs|
|`GDMENUCardManager.Core`|net6.0|Shared logic: `Manager.cs` (main card manager), `Redump2CdiConverter.cs`, `TosecDatLookup.cs`, PVR codecs under `PuyoTools/VrSharp/`, `gamedb.json`; data assets copied to output (`tools/`, `runtimes/`, blacklists)|

- `GDMENUCardManager/SevenZip/SevenZip.csproj` (netstandard2.0;net45) is not in the solution and nothing references it.

- Both app projects `Import` `Core/libchdr.targets` + `Core/redump2cdi.targets`: they inject the platform-native `libchdr` / `redump2cdi` binaries from `Core/runtimes/<rid>/native/` into **publish** output. Dev `dotnet run` on Windows uses `Core/runtimes/win-x64/native/libchdr.dll` (csproj Content item).
- WPF project defines `UNMANAGED` constant + `ProduceReferenceAssembly=false`; Release disables debug symbols.

## Dark mode (WPF UI) — non-obvious traps

- The WPF app runs the WPF-UI 4.3.0 dark theme: `App.xaml` merges `ui:ThemesDictionary Theme="Dark"` + `ui:ControlsDictionary`, then re-declares implicit styles that pin compact metrics (12px font, small padding, `MinHeight=0`).
- **An implicit style in `App.xaml` replaces WPF-UI's implicit style, and `BasedOn="{StaticResource {x:Type X}}"` does not bring it back, for any control.** The lib ships an implicit style for Button, RepeatButton, CheckBox, RadioButton, Label, TextBox and ComboBox, but with that `BasedOn` the app style's `Style.BasedOn` ends up null (observed at runtime), so the control falls back to the built-in Aero2 template: light-blue `#BEE6FD` button hover, white TextBox, classic CheckBox/RadioButton. Button, RepeatButton and ComboBox name the lib's keyed style (`DefaultButtonStyle`, `DefaultRepeatButtonStyle`, `DefaultComboBoxStyle`) and are dark. Label, CheckBox, RadioButton and TextBox are deliberately left on Aero2 (compact metrics, no `BasedOn`) with theme brushes set on top: `Foreground` on all four, plus `Background`/`BorderBrush`/`CaretBrush` on TextBox (the default caret is derived from the background color).
- No global `TextBlock` style: it would override the disabled-text color inside control templates. Each window sets `Foreground="{DynamicResource TextFillColorPrimaryBrush}"` next to its `Background` instead; a new window needs both.
- DataGrid `dg1` (MainWindow): header uses the lib's `DefaultDataGridColumnHeaderStyle`; column separators need `GridLinesVisibility="All"` (a `"Horizontal"` setting silently drops the vertical bars; both brushes are `ControlElevationBorderBrush`).
- Dark DWM title bar: P/Invoke `DwmSetWindowAttribute(20)` in `App.xaml.cs`, applied to every window through a class handler on `Window.Loaded`; no-op on older Windows.

## Verifying UI state (hover, colors) without a human at the mouse

- Run the built app, then: `GetWindowRect` + GDI `CopyFromScreen` for a PNG; move the OS mouse with `SetCursorPos` to trigger hover/focus; `diff`/compare pixel values between captures. Works from any .NET console probe app (`SetCursorPos` needs no focus-stealing; 300–600 ms after the move before capturing).
- Beware: two separate captures of a live window can differ by ±1–2 levels from DWM recompose noise — judge changes by dominant color shifts / bounding boxes, not by "any pixel changed".

## Build & publish (from README, useful for distribution)

- Multi-platform publish is the Avalonia project:
  - `dotnet publish src/GDMENUCardManager.AvaloniaUI/GDMENUCardManager.AvaloniaUI.csproj -c Release -r linux-x64`
  - self-contained single file: add `--self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`
  - `osx-arm64` / `osx-x64` / `win-x64` similarly; chmod +x `GDMENUCardManager` and `tools/redump2cdi` on Unix.
- Windows WPF distribution: `dotnet publish src/GDMENUCardManager/GDMENUCardManager.csproj -c Release -r win-x64 --self-contained false` — framework-dependent (needs the .NET 8 Desktop Runtime, see `docs/dotnetruntime.png`); publish output has `libchdr.dll` at root + `tools/redump2cdi.exe` (from the `.targets` imports). Zip with a top-level `GDMENUCardManager-win-x64/` folder (~24 MiB).
