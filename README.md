<div align="center">

<img src="docs/luma-mark.svg" width="96" alt="Luma logo" />

# ✨ Luma

### Less light. More play.

Your Xbox controller does not need to be a floodlight. 🎮🌙

**Windows 11 · WinUI 3 · .NET 10 · English / Deutsch · MIT**

</div>

---

## 🎮 A small app for more comfortable gaming

Luma adjusts the Xbox logo LED from a compact native Windows window or the system tray. Built with Codex as a vibe-coding project for evenings when even a single LED feels too bright.

| Feature | What you get |
| --- | --- |
| 🌙 Brightness | 0–100% slider and Night, Relaxed, and Full presets |
| 💡 Light modes | On/off, steady light, pulses, and blinking |
| 🍃 System tray | A green Luma icon with quick actions and a Quit command |
| 🔁 Reconnect | Automatically apply the saved setting when a controller reconnects |
| 🚀 Startup | Optionally start with Windows, hidden in the tray |
| 🖥️ Compact UI | Small controls and settings directly in the main window |
| 🌍 Languages | German on German Windows installations; English otherwise |

> **Connection:** USB or the Microsoft Xbox Wireless Adapter. This LED control method does not support Bluetooth.

## 🚀 Quick start

1. Download the Windows x64 ZIP from [Releases](https://github.com/Xploder/Luma/releases/latest).
2. Extract it and run **Luma.exe**. The runtime is bundled; a separate .NET installation is not required.
3. Connect your controller, choose a brightness, and enjoy a less distracting LED.

Close other LED-control utilities if they keep overwriting your setting. Steam is not required. Preserve the included license files when sharing the app.

## 🎛️ Controls and settings

- **LED switch:** turn the light off while keeping the saved brightness.
- **Brightness:** 0–100% maps to the supported protocol range of 0–47. This is the requested setting, not a measurement read back from the controller.
- **Light mode:** steady, slow/fast pulse, slow/fast blink, charging blink, or fade in. Firmware support varies.
- **Mouse wheel:** scroll the main window over controls and empty areas. Scrolling over the brightness slider or a closed dropdown does not change its value. Open dropdown lists retain their own scrolling.
- **Hide to tray:** hide the window and reopen it from the green Luma icon. Right-click for presets, LED on/off, and Quit. Windows may initially put the icon in its tray overflow menu.
- **Automatic discovery:** Off, 8 / 15 / 30 / 60 seconds, or 5 minutes. The default remains 8 seconds. Each interval starts after the previous search finishes; searches never overlap. Changes are saved and take effect immediately.
- **Off:** stops the discovery timer entirely, including in the tray. A single startup search and the manual refresh button remain available. A search already in progress is allowed to finish. Reconnected controllers are detected only on the next manual search or app restart.
- **Apply when connected:** apply the shared saved setting to newly detected controllers. This depends on discovery; it does not run a separate polling loop.
- **Start with Windows:** optional and initially disabled. This creates a startup entry for the current Windows user. After moving the app, toggle this setting off and back on to update its path.
- **Keep running in tray when closed:** close the window without quitting. Use **Quit** to exit completely.

Settings are stored in `%LOCALAPPDATA%\LumaXboxLed\settings.json`. One shared default intentionally survives changing XboxGIP device IDs. Manual edits affect the selected controller; newly detected controllers receive the shared default when automatic application is enabled.

### Idle performance

Luma sends LED commands when settings change or when a newly detected controller needs its saved setting. It does not continuously resend brightness. With automatic discovery off, no periodic controller searches or app-owned repeating timers run while idle. Windows, WinUI and .NET still consume memory and may perform occasional work; this is not a guarantee of literally zero CPU use. Tray mode hides the interface, and foreground-only mouse input is not captured while another application is active. The wheel input path uses a stack buffer to avoid per-movement heap allocations with high-polling-rate mice. You can quit Luma after applying a setting; automatic reapplication on reconnection then requires starting it again.

## 🌍 Language support

The app follows the Windows display language using .NET UI culture selection. German variants such as `de-DE` and `de-AT` use German resources. English is the default and the fallback for other languages. This includes the interface, tray menu, status messages, accessibility labels, and app-authored errors. Error text originating from Windows itself may use the operating system's language.

Translations live in `Resources/Strings.resx` (English) and `Resources/Strings.de.resx` (German). GitHub documentation and release descriptions are maintained in English.

## 📦 Compact distribution

The self-contained executable is approximately **34 MiB**. The ZIP contains the EXE, a short guide, and two license files. On first launch, runtime files are extracted under `%TEMP%\.net\Luma` and reused. The 1.1 build used approximately **64 MiB** for this cache, giving about **98 MiB** total for the EXE and its active cache instead of the original 232 MiB program folder. Exact sizes vary between versions; old version caches may take additional space.

Luma references the specific WinUI package rather than the full Windows App SDK, uses partial .NET trimming, and bundles the result into a compressed executable. Settings use source-generated JSON serialization.

## 🛠️ Build it yourself

Requirements: Windows x64 and the .NET 10 SDK. Windows build tools are restored through NuGet.

```powershell
.\build.ps1           # Release build
.\build.ps1 -Publish  # Portable build in dist/Luma-Windows-x64
# Protocol, settings, and language checks:
dotnet run --project .\Tests\Luma.Tests.csproj -c Release
```

`Luma.exe --demo` starts a demo without hardware commands or settings changes. `Luma.exe --tray` starts hidden in the system tray.

The backend opens Microsoft's existing `\\.\XboxGIP` interface. It does not install replacement drivers or process/emulate game input. The access method was reverse engineered by xbledctl. Successful transmission does not guarantee that every firmware supports every animation.

## 🧪 Validation and limitations

The tests cover a reference 23-byte protocol packet, LED-off behavior, mode validation, full 64-bit device IDs, brightness bounds, settings serialization, and German/English/fallback resource selection. The compact executable has also been checked with the real WinUI interface and controller discovery.

The trimmer reports warnings in Microsoft's Windows SDK and WinRT projections. Compatibility with every Windows build and controller firmware has not been exhaustively tested.

## 💚 Thanks to the original project

A huge thank-you to **[Leclowndu93150](https://github.com/Leclowndu93150)** and **[xbledctl](https://github.com/Leclowndu93150/xbledctl)**!

xbledctl did the essential work to make Xbox controller LED control accessible on Windows. Luma draws on its idea, reverse-engineered XboxGIP access, LED command, and packet format. The backend was adapted to C#; Luma's WinUI interface and interaction design were developed separately.

The original work belongs to its authors. Its full MIT license and copyright notice are preserved in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt), and [GipController.cs](GipController.cs) documents the provenance. Visit the original project for the compact C/C++ implementation and technical research.

Thanks also to the teams behind **WinUI**, **Windows App SDK**, and **.NET**.

## 📄 License

Luma is licensed under [MIT](LICENSE), copyright 2026 Xploder. The adapted xbledctl code retains its original MIT notice. Bundled Microsoft runtime components remain subject to their respective licenses.

## Scrolling implementation

The main page uses WinUI `ScrollView` with a separate vertical scrollbar. The scrollbar remains visible whenever the content is taller than the viewport, including at the bottom of the page, and disappears once all content fits.

The active window receives mouse-wheel input through Windows Raw Input, so scrolling remains available over controls that consume WinUI wheel events. Registration is limited to foreground input; no global hook or background input capture is used. Wheel input is applied only while the pointer is over Luma, and open dropdown lists keep their own input handling. Native window subclasses and WinUI pointer handlers provide additional input handling and prevent duplicate page scrolling. Rapid deltas are accumulated while scrolling completes asynchronously.

For troubleshooting, start `Luma.exe --scroll-diagnostics` to enable a local `Luma-scroll.log` beside the executable. It records wheel deltas and scroll dimensions, but no pointer positions, controller IDs, or input from other applications. Logging is disabled during normal use.

## High-DPI display support

Luma declares Per-Monitor V2 DPI awareness and enables layout rounding. This prevents Windows bitmap scaling of the interface on high-DPI monitors. Version 1.3.1 was visually checked on a 4K display at 150% Windows scaling, including scrolling.
