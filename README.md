# Hamster Desktop

An unofficial Windows desktop wrapper for [aBowman's classic Hamster widget](https://www.abowman.com/gadgets/hamster/).

Feed the hamster, watch it run on its wheel, and keep its little home on your desktop. The original Flash animation runs through [Ruffle](https://ruffle.rs/), with a native Windows window powered by WebView2.

## Features

- Original feeding, drinking, and wheel interactions.
- Draggable window with an always-on-top option.
- Three window sizes and pause/resume controls.
- System tray support, with animation paused while hidden.
- Saved window position, size, and pin preference.
- Single-executable local builds with offline playback.

## Requirements

- Windows 10 or 11, 64-bit.
- .NET Framework 4.6.2 or later.
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/), installed separately.

The current application interface is in Simplified Chinese.

## Building

The build script uses the .NET Framework C# compiler included with Windows. Python, Node.js, and Visual Studio are not required.

Prepare the following dependencies in your local working directory:

| Path | Dependency |
| --- | --- |
| `assets/hamster.swf` | The original aBowman animation. Confirm the applicable permissions before obtaining or using it. |
| `assets/ruffle/` | Ruffle self-hosted web player, nightly `2026-05-04`, including its license file. |
| `vendor/webview2/` | The extracted [Microsoft.Web.WebView2 1.0.4258.31 NuGet package](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31). |

The original SWF and local SDK directory are excluded from version control. A fresh checkout requires these dependencies before it can build. The script does not download the original animation.

From the project directory, run:

```powershell
.\build.ps1
```

The executable is written to `dist/`. The build embeds the local animation, player, and SDK libraries into the executable; the WebView2 Runtime remains a separate prerequisite.

## Usage

After building locally, double-click the executable in `dist/` to open the widget.

| Action | Result |
| --- | --- |
| Click an empty area | Drop food for the hamster. |
| Click the wheel's center | Send the hamster back to its wheel. |
| Drag the title bar | Move the window. |
| Click the pin control | Toggle always-on-top. |
| Open `···` | Change size, pause/resume, or reload. |
| Click `−` | Hide the widget in the system tray. |
| Double-click its tray icon or launch it again | Restore the existing window. |
| Click `×` | Exit. |

On first launch, bundled resources are extracted to `%LOCALAPPDATA%\HamsterDesktop\runtime`. Settings and a dedicated WebView2 profile are stored under `%LOCALAPPDATA%\HamsterDesktop`; your personal browser profile is not used.

To remove the application, exit it and delete the executable and its data directory.

## Project Structure

```text
src/                       Native window, tray controls, and resource loading
assets/index.html          Local player page
assets/widget.js           Playback controls and desktop communication
assets/ruffle/             Ruffle player and license
build.ps1                  Build script and application icon drawing
THIRD-PARTY-NOTICES.txt     Third-party attribution and license details
```

## Verification

Run the built executable with `--self-test` to check offline loading, pause/resume, resizing, pinning, and hide/restore behavior:

```powershell
$app = Get-ChildItem .\dist\*.exe | Select-Object -First 1
& $app.FullName --self-test .\qa\local-check
```

The check uses a separate browser profile and writes a JSON report and preview image to the specified directory.

## Copyright and Licensing

### Original Hamster Widget

The hamster artwork, animation, and original `hamster.swf` are by **aBowman** and remain the property of their respective rights holders. The SWF is used without modification. This project is not affiliated with or endorsed by aBowman.

As of **October 21, 2026**, the maintainer has **not contacted the original author or obtained separate permission** to redistribute the original animation or offline packages containing it. The [original page](https://www.abowman.com/gadgets/hamster/) displays a site-wide **© 2026 aBowman** notice and provides iframe embed code. No explicit license covering this project's offline redistribution was found in the pages reviewed; the embed option should not be treated as that permission.

The original SWF and compiled packages containing it are excluded from the intended public repository. **Confirm the necessary permissions before publishing them, including as GitHub Releases.** Attribution or noncommercial distribution alone does not establish permission.

### Desktop Wrapper and Dependencies

No standalone open-source license has been selected for the newly written desktop wrapper. This README does not grant rights to the original animation or replace third-party licenses.

- **Ruffle:** MIT OR Apache-2.0. See [the included license](assets/ruffle/LICENSE.md).
- **Microsoft WebView2 SDK:** governed by the `LICENSE.txt` and `NOTICE.txt` supplied with the SDK. These are included in the resource bundle when building locally.
- **Microsoft Edge WebView2 Runtime:** provided separately by Microsoft and not bundled with this project.

See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for attribution, source links, and the recorded authorization status.
