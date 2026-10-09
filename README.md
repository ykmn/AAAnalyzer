# AAAnalyzer

[🇷🇺 Русский](README-ru.md) · 🇬🇧 English

A portable sound analyzer for Windows: shows the spectrum, loudness and phase
of system audio or a network stream (Icecast / HLS) in real time. Nothing to
install: no driver, no virtual audio cable.

## Features

### Audio sources

- **Device** — the system mix of the selected output device via WASAPI loopback. Playback on speakers or headphones is not interrupted. ASIO is not supported! A **Pre/Post fader** toggle chooses whether the system volume of the output device affects the measured level.
- **Stream** — an HTTP/HTTPS Icecast or HLS (`.m3u8`) URL, decoded by LibVLC. A built-in URL library stores stream addresses and exports them to `.m3u8`.

### Level meter panel (left)

- Peak meters for the L and R channels with a dB scale.
- Integrated LUFS loudness meter (LU). Its vertical scale matches the Loudness chart scale: 0 and the minimum are at the same heights, with marks every 3 LU.
- Phase meter (L/R correlation from −1 to +1).

### Modes (tabs)

- **Waterfall** — stereo waterfall: left channel on top, right channel at the bottom. After a stop/restart the pause stays as a black gap (the Loudness chart also shows a gap in the line).

![Waterfall](screenshots/AAAnalyzer-Waterfall.png)

- **Loudness** — loudness history chart over time.
  - Metrics: Momentary, Short-Term, Integrated.
  - Window: 1 min … 12 h; Y scale (Y−/Y+), shift (▼/▲) or auto-scale.
  - Target line (−23 LU by default) with a ±3 LU tolerance band.
  - Calculation per ITU-R BS.1770 (K-weighting, absolute and relative gates).

![Loudness: Momentary](screenshots/AAAnalyzer-LoudnessM.png)

![Loudness: Short-term](screenshots/AAAnalyzer-LoudnessS.png)

![Loudness: Integrated](screenshots/AAAnalyzer-LoudnessI.png)

- **RTA** — spectrum analyzer
  - 1/1, 1/3, 1/6, 1/12 octave bands
  - Mono / L / R source,
  - averaging (1, 10, 20, …).
  - Target line (−23 LU by default) with a ±3 LU tolerance band

![RTA](screenshots/AAAnalyzer-RTA.png)

- **Phase** — phase scope with gain control.

![Phase](screenshots/AAAnalyzer-Phase.png)

Buttons in the tab row: **RESET** (all measurements and charts), **PNG**
(screenshot of the current tab into `Screenshots\`), **SETTINGS**.

### Interface language

English or Russian: the switch is at the top of the settings window and applies immediately.
The choice is saved in `Data\App.json`. On first launch the system language is used.

### Settings and profiles

The settings window covers:
  - FFT (size, window, scales),
  - Waterfall (thresholds and palette),
  - meters,
  - Loudness,
  - RTA,
  - Phase.
Sets of settings are stored in profiles (`Data\profiles.json`): you can save one under a name, delete it, or make it the default. Changes made with the panel buttons (metric, window, target, RTA)
are saved to the default profile automatically.

## Requirements

- Windows 10/11.
- Nothing is needed to run a ready-made build (.NET and LibVLC are included).
- To build from source: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
  with Windows Desktop (WPF) support.

## Running a ready-made build

Unpack the `AAAnalyzer-<version>.zip` archive anywhere and run
`AAAnalyzer.exe`. The following are created next to the program:

| Path | Contents |
|---|---|
| `Data\profiles.json` | settings profiles |
| `Data\App.json` | interface language |
| `Data\stream-history.json` | stream URL history |
| `logs\AAAnalyzer.log` | run log |
| `Screenshots\` | PNG screenshots of tabs |

## Building from source

```powershell
git clone https://github.com/ykmn/AAAnalyzer
cd AAAnalyzer

# debug build and tests
dotnet build
dotnet test

# run without publishing
dotnet run --project src\SystemAudioAnalyzer.App
```

### Portable build (.exe)

```powershell
# the version is taken from VERSION.txt, format "1.012 - 2026.10.08"
.\release\rebuild.ps1                              # win-x64
.\release\rebuild.ps1 -RuntimeIdentifier win-arm64 # ARM64
```

The script runs `dotnet publish` (Release, self-contained, not single-file) into
`release\AAAnalyzer-<version>`, removes unneeded localizations (keeping only `en` and `ru`)
and checks that the build contains `AAAnalyzer.exe` and `libvlc.dll`. The finished folder
can be copied to another computer as a whole.

Change history is in [CHANGELOG.md](CHANGELOG.md).

## Repository layout

```text
SystemAudioAnalyzer.sln
src/
  SystemAudioAnalyzer.Core/        engine: capture, levels, True Peak, LUFS, FFT
  SystemAudioAnalyzer.App/         WPF UI, WASAPI and LibVLC sources, settings
  SystemAudioAnalyzer.Diagnostic/  console utility: list output devices
tests/
  SystemAudioAnalyzer.Core.Tests/  engine and meter tests
  SystemAudioAnalyzer.App.Tests/   tests for UI logic, settings and rendering
release/
  rebuild.ps1                      portable build script
```

### How the data flows

```text
WASAPI loopback / LibVLC
  → queue (~4 s of audio)
  → equal chunks, played back on the audio clock (~30 frames/s)
  → LevelMeter, TruePeak, LoudnessMeter, SpectrumAnalyzer (FFT)
  → AnalysisFrame (immutable snapshot)
  → WPF views
```

Network sources deliver audio in bursts (about half a second each), so the engine
plays it back at audio pace rather than as it arrives: frames come out evenly
and no audio is lost.

## Acknowledgments

The interface idea was inspired by the Spectrum Tool DSP plugin for WinAMP.
