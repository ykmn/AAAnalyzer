# Full Measurement Settings and Profiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement functional reference-aligned settings for every analyzer panel, live engine reconfiguration, and persistent shared profiles.

**Architecture:** Keep immutable settings snapshots and profile persistence in App; keep FFT/window DSP configuration in Core. The analysis worker adopts configuration only at buffer/frame boundaries. Renderers consume the active snapshot, while the settings dialog edits drafts and separates Apply from profile persistence.

**Tech Stack:** .NET 8, WPF, C#, NAudio, System.Text.Json, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-07-full-measurement-settings-design.md`

## Global Constraints

- Target .NET 8 WPF on Windows; Core must not reference WPF or App.
- Apply active changes without stopping capture, playback, or the selected source.
- The capture callback must not perform disk I/O, WPF work, or blocking configuration changes.
- Store settings and profiles in `<exe>/Data`; keep logs and screenshots in their existing locations.
- Load the Default profile at startup; preserve the legacy `%LocalAppData%\AAAnalyzer\settings.json` during one-time import.
- `Apply` changes runtime only; `Save` persists the selected profile; `Save as` creates a profile; only explicit `Default` changes the startup profile.
- Portable localization output retains only `ru` and `en`.

## Review Focus

- An invalid, truncated, or future-version profile catalog must not prevent startup; test fallback diagnostics and preservation of the source file.
- Numeric inputs at bounds, NaN/infinity, duplicate gradient levels, and malformed colors must be rejected before applying or persisting; test `MeasurementSettingsValidator` and editor validation.
- Rapid FFT changes while buffers are arriving must keep source state Running and leave no mixed-size spectrum; test frame-boundary adoption.
- Changing frequency-scale mode or RTA resolution must reset only incompatible histories and preserve unrelated settings; test renderer/engine state transitions.
- App running from a read-only install directory must report a profile write failure without corrupting the last saved catalog; test store behavior with an unwritable path where supported.

---

### Task 1: Expand and validate the immutable settings model

**Files:**
- Modify: `src/SystemAudioAnalyzer.App/Settings/MeasurementSettings.cs`
- Create: `src/SystemAudioAnalyzer.App/Settings/MeasurementSettingsValidator.cs`
- Create: `src/SystemAudioAnalyzer.App/Rendering/ColorGradient.cs`
- Modify: `tests/SystemAudioAnalyzer.App.Tests/SettingsStoreTests.cs`
- Create: `tests/SystemAudioAnalyzer.App.Tests/MeasurementSettingsValidatorTests.cs`

**Interfaces:**
- Produces `MeasurementSettingsValidator.Validate(MeasurementSettings settings) -> IReadOnlyList<string>` and `MeasurementSettingsValidator.IsValid(MeasurementSettings settings) -> bool`.
- `MeasurementSettings` adds the Analyzer FFT/window/scales/gain fields, Waterfall gradient stops, meter dynamics/visibility/colors/LUFS scale options, Loudness gradient stops, and RTA averaging/tilt/release/hold/colors specified by the reference screenshots. Color stops are immutable records with `double LevelDb` and `string Color`.

- [ ] **Step 1: Write failing model and validation tests.** Assert reference defaults (FFT 2048, Blackman, linear frequency/log amplitude, -130 dB Analyzer floor, Waterfall stops -110/-80/-55/-45 dB with colors `#000000/#0080C0/#00FF39/#E8E800`), valid sorted gradients, midpoint interpolation between two known color stops, and rejection of NaN, invalid enums, invalid colors, unordered or duplicate stops, and out-of-range FFT size.
- [ ] **Step 2: Run** `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~MeasurementSettingsValidatorTests` **and verify the new tests fail because the model/validator is missing.**
- [ ] **Step 3: Implement the immutable records and validator.** Keep every field JSON-serializable and ensure each gradient contains at least two strictly increasing finite levels.
- [ ] **Step 4: Run** `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter "FullyQualifiedName~MeasurementSettingsValidatorTests|FullyQualifiedName~SettingsStoreTests"` **and verify PASS.**
- [ ] **Step 5: Commit** the settings model and tests as `feat: expand measurement settings model`.

### Task 2: Reconfigure Core spectrum processing on the worker

**Files:**
- Create: `src/SystemAudioAnalyzer.Core/AnalysisConfiguration.cs`
- Modify: `src/SystemAudioAnalyzer.Core/SpectrumAnalyzer.cs`
- Modify: `src/SystemAudioAnalyzer.Core/AudioAnalysisEngine.cs`
- Modify: `src/SystemAudioAnalyzer.App/Services/IAnalyzerController.cs`
- Modify: `src/SystemAudioAnalyzer.App/Services/AnalyzerController.cs`
- Modify: `tests/SystemAudioAnalyzer.Core.Tests/SpectrumAnalyzerTests.cs`
- Modify: `tests/SystemAudioAnalyzer.Core.Tests/AudioAnalysisEngineTests.cs`
- Modify: `tests/SystemAudioAnalyzer.App.Tests/AnalyzerControllerTests.cs`

**Interfaces:**
- `AnalysisConfiguration` is an immutable Core record containing `int FftSize` and `SpectrumWindow Window` (`Rectangular`, `Hann`, `Hamming`, `Blackman`).
- `SpectrumAnalyzer(int fftSize, SpectrumWindow window)` applies the selected window to mono and stereo spectra.
- `AudioAnalysisEngine.SetAnalysisConfiguration(AnalysisConfiguration configuration)` stores the latest requested configuration; `IAnalyzerController.SetAnalysisConfiguration(AnalysisConfiguration configuration)` forwards it.

- [ ] **Step 1: Add failing tests** for each window function, 512–16384 power-of-two FFT values, rejection of invalid sizes, and FFT metadata after a live update.
- [ ] **Step 2: Run** `dotnet test tests/SystemAudioAnalyzer.Core.Tests --filter FullyQualifiedName~SpectrumAnalyzerTests` **and verify failure on unsupported window/configuration behavior.**
- [ ] **Step 3: Implement `AnalysisConfiguration` and window selection**; preserve Hann as the compatibility default for direct Core callers.
- [ ] **Step 4: Add a latest-requested configuration slot** to `AudioAnalysisEngine`; at the start of worker buffer processing, compare and replace the worker-owned `SpectrumAnalyzer`, clearing only FFT accumulation. Do not lock or reconfigure in `OnSamplesAvailable`.
- [ ] **Step 5: Add a live-running engine test** that requests multiple configurations during sample delivery, verifies Running remains true, and verifies emitted FFT metadata matches the adopted configuration without restarting the fake source.
- [ ] **Step 6: Run** `dotnet test tests/SystemAudioAnalyzer.Core.Tests` **and** `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~AnalyzerControllerTests`; verify PASS.
- [ ] **Step 7: Commit** as `feat: apply spectrum configuration live`.

### Task 3: Implement preset catalog, migration, and explicit profile operations

**Files:**
- Create: `src/SystemAudioAnalyzer.App/Settings/SettingsProfile.cs`
- Create: `src/SystemAudioAnalyzer.App/Settings/SettingsProfileCatalog.cs`
- Modify: `src/SystemAudioAnalyzer.App/Settings/SettingsStore.cs`
- Modify: `src/SystemAudioAnalyzer.App/App.xaml.cs`
- Modify: `src/SystemAudioAnalyzer.App/SystemAudioAnalyzer.App.csproj`
- Modify: `tests/SystemAudioAnalyzer.App.Tests/SettingsStoreTests.cs`
- Create: `tests/SystemAudioAnalyzer.App.Tests/SettingsProfileCatalogTests.cs`
- Modify: `tests/SystemAudioAnalyzer.App.Tests/PortableBuildTests.cs`

**Interfaces:**
- `SettingsProfile(string Id, string Name, MeasurementSettings Settings)` and `SettingsProfileCatalog(int SchemaVersion, string DefaultProfileId, IReadOnlyList<SettingsProfile> Profiles)` are immutable.
- `SettingsStore(string? dataDirectory = null, Action<string>? diagnostic = null)` defaults to `Path.Combine(AppContext.BaseDirectory, "Data")` and exposes `LoadCatalogAsync`, `SaveCatalogAsync`, and `LoadStartupSettingsAsync`.
- Catalog mutation helpers are pure: `SaveProfile`, `SaveAsProfile`, `DeleteProfile`, and `SetDefaultProfile` return a validated new catalog.

- [ ] **Step 1: Write failing tests** for Default startup selection, profile save/save-as/delete/default, refusing to delete Default, atomic temp-file writes, invalid catalog fallback, one-time field-by-field migration from the old LocalAppData `settings.json` without deleting it, and a failed write that leaves the previous catalog readable.
- [ ] **Step 2: Run** `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~SettingsProfileCatalogTests` **and verify failure before implementation.**
- [ ] **Step 3: Implement profile records and pure catalog operations**; reject duplicate IDs/names and any invalid settings snapshot.
- [ ] **Step 4: Implement versioned JSON storage under `Data/profiles.json`** using temp-file plus replace; on read/validation failure log and load built-in Default. Import legacy settings only when no catalog exists and leave the source file untouched.
- [ ] **Step 5: Wire startup to load the Default profile** and configure app data publishing so `Data` is available beside the executable without relocating logs/screenshots.
- [ ] **Step 6: Run** `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter "FullyQualifiedName~SettingsProfileCatalogTests|FullyQualifiedName~SettingsStoreTests|FullyQualifiedName~PortableBuildTests"`; verify PASS and that the legacy fixture remains present.
- [ ] **Step 7: Commit** as `feat: persist shared measurement profiles`.

### Task 4: Make meter and loudness controls affect measurement and rendering

**Files:**
- Modify: `src/SystemAudioAnalyzer.Core/LoudnessMeter.cs`
- Modify: `src/SystemAudioAnalyzer.Core/LoudnessMeasurement.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/MeterRailView.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/MeterRailLayout.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/MeteringHistory.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/LoudnessView.cs`
- Modify: `src/SystemAudioAnalyzer.App/ViewModels/MainViewModel.cs`
- Modify: `tests/SystemAudioAnalyzer.App.Tests/MeterRailLayoutTests.cs`
- Modify: `tests/SystemAudioAnalyzer.App.Tests/MeteringHistoryTests.cs`
- Modify: `tests/SystemAudioAnalyzer.Core.Tests/LoudnessMeterTests.cs`
- Create: `tests/SystemAudioAnalyzer.App.Tests/MeterRenderingTests.cs`
- Create: `tests/SystemAudioAnalyzer.App.Tests/LoudnessGradientTests.cs`

**Interfaces:**
- `MeteringHistory.Update(ChannelLevel level, DateTimeOffset timestamp, MeterSettings settings) -> MeterDisplayState` owns per-channel attack/release smoothing and expiring maximum hold.
- `LoudnessMeter` accepts a bounded integrated-window duration while preserving Momentary and Short-term windows; `LoudnessMeasurement` continues to expose all three metrics.
- `LoudnessView` and `MeterRailView` continue to consume `MeasurementSettings` snapshots; paint methods use shared color-gradient interpolation.

- [ ] **Step 1: Write failing tests** asserting non-zero fill geometry for non-silent current True Peak, zero fill for silence, RMS visibility/color, peak hold expiry, attack/release response, dB-scale visibility, selected LUFS metric, bounded rolling integrated duration, and color interpolation between the reference LUFS stops.
- [ ] **Step 2: Run** `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter "FullyQualifiedName~MeterRenderingTests|FullyQualifiedName~MeteringHistoryTests|FullyQualifiedName~LoudnessGradientTests"` and `dotnet test tests/SystemAudioAnalyzer.Core.Tests --filter FullyQualifiedName~LoudnessMeterTests`; verify the new behavior is missing.
- [ ] **Step 3: Implement meter display state** with timestamp-based attack/release and hold decay; derive bar geometry from finite clamped dB values and preserve independent overload reset. Ensure silence, negative/invalid levels, and timestamps moving backwards produce finite safe geometry.
- [ ] **Step 4: Implement conditional peak/RMS/dB/LUFS/LKFS rendering** with configured colors and selected LUFS metric; keep settings immutable and never do measurement work in the paint callback beyond current-frame mapping.
- [ ] **Step 5: Add a reusable gradient sampler** and render Loudness history with configured stops, window, center, span, and metric.
- [ ] **Step 6: Run** `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter "FullyQualifiedName~Meter|FullyQualifiedName~Loudness"` and `dotnet test tests/SystemAudioAnalyzer.Core.Tests --filter FullyQualifiedName~LoudnessMeterTests`; verify PASS.
- [ ] **Step 7: Commit** as `feat: apply meter and loudness display settings`.

### Task 5: Apply Analyzer, Waterfall, RTA, and Phase options

**Files:**
- Modify: `src/SystemAudioAnalyzer.App/Rendering/RealtimeAnalyzerView.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/WaterfallView.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/WaterfallRenderer.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/RtaBandAggregator.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/RtaRenderer.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/RtaView.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/PhaseScopeView.cs`
- Modify: `src/SystemAudioAnalyzer.App/ViewModels/MainViewModel.cs`
- Modify: corresponding `FrequencyScaleTests`, `WaterfallLayoutTests`, and `RtaRenderingTests`

**Interfaces:**
- The Task 1 `ColorGradient.Sample(IReadOnlyList<ColorStop> stops, double levelDb) -> Color` returns clamped endpoint/interpolated colors.
- `RtaBandAggregator` accepts averaging count and resets only bin history when source/resolution changes; `RtaView` applies tilt, release in dB/second, target band, and expiring peak caps.

- [ ] **Step 1: Add failing tests** for log and linear frequency mapping, equal L/R normalized coordinates, Waterfall floor/offset gradient sampling, RTA bounded rolling averages, 1 kHz-zero tilt, release decay and cap expiry, and Phase gain mapping.
- [ ] **Step 2: Run** `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter "FullyQualifiedName~FrequencyScaleTests|FullyQualifiedName~WaterfallLayoutTests|FullyQualifiedName~RtaRenderingTests"`; verify the unimplemented settings fail.
- [ ] **Step 3: Implement gradient sampling and Waterfall palette rendering** from sorted dB stops; share Analyzer frequency scale and cursor mapping across L/R.
- [ ] **Step 4: Implement Analyzer display scale/gain/window metadata and pass `AnalysisConfiguration` through `MainViewModel` to `IAnalyzerController`; active settings update without changing source state.**
- [ ] **Step 5: Implement RTA averaging, tilt, release, peak-cap hold, target band, and configured colors**; reset only bins/peaks invalidated by changed source or resolution.
- [ ] **Step 6: Apply Phase gain and Analyzer cursor/text colors consistently; verify geometry and render tests.**
- [ ] **Step 7: Run** `dotnet test tests/SystemAudioAnalyzer.App.Tests`; verify PASS.
- [ ] **Step 8: Commit** as `feat: apply analyzer waterfall and rta settings`.

### Task 6: Rebuild settings dialog, draft workflow, and standard color chooser

**Files:**
- Modify: `src/SystemAudioAnalyzer.App/ViewModels/SettingsDialogViewModel.cs`
- Modify: `src/SystemAudioAnalyzer.App/Views/SettingsWindow.xaml`
- Modify: `src/SystemAudioAnalyzer.App/Views/SettingsWindow.xaml.cs`
- Modify: `src/SystemAudioAnalyzer.App/SystemAudioAnalyzer.App.csproj`
- Modify: `tests/SystemAudioAnalyzer.App.Tests/SettingsEditSessionTests.cs`
- Modify: `tests/SystemAudioAnalyzer.App.Tests/SettingsStoreTests.cs`
- Create: `tests/SystemAudioAnalyzer.App.Tests/SettingsDialogViewModelTests.cs`

**Interfaces:**
- `SettingsDialogViewModel` exposes `SelectedProfileId`, `Profiles`, `HasUnsavedDraft`, `SelectProfile`, `Apply`, `SaveAsync`, `SaveAsAsync`, `SetDefaultAsync`, `DeleteAsync`, and `Cancel`.
- `Apply` emits an applied runtime snapshot but does not call persistence; `SaveAsync` persists only the selected profile. Window receives an injected `Func<string, string?>` color-picker delegate for testability; production implementation opens the Windows common color dialog.

- [ ] **Step 1: Write failing tests** for dirty-draft confirmation, profile switching, Apply-without-save, explicit Save/Save As/Default/Delete, Cancel restoring the opening runtime snapshot, and color chooser results changing only selected stop.
- [ ] **Step 2: Run** `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~SettingsDialogViewModelTests`; verify they fail before dialog workflow implementation.
- [ ] **Step 3: Implement transactional draft state** and profile operations; require explicit confirmation before discarding a dirty draft and preserve the runtime snapshot from dialog opening on Cancel.
- [ ] **Step 4: Add a standard Windows color chooser** (Windows Forms `ColorDialog`; enable WPF/WinForms interop only in App) and bind it to color and gradient swatch actions.
- [ ] **Step 5: Rebuild the SettingsWindow to match reference pages** including common preset header, Analyzer/Waterfall/Meters/Loudness/RTA/Phase pages, dB stop list, Add/Delete/Color buttons, Apply/Save/Save As/Default/Delete/OK/Cancel behavior, and validation feedback.
- [ ] **Step 6: Run** `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter "FullyQualifiedName~SettingsDialogViewModelTests|FullyQualifiedName~SettingsEditSessionTests|FullyQualifiedName~SettingsStoreTests"`; verify PASS.
- [ ] **Step 7: Commit** as `feat: implement settings profiles dialog`.

### Task 7: End-to-end regression and portable build verification

**Files:**
- Modify: `tests/SystemAudioAnalyzer.App.Tests/PortableBuildTests.cs`
- Modify: `tests/SystemAudioAnalyzer.App.Tests/StartupAndLoggingTests.cs`
- Modify: `TODO.md`
- Modify: `DONE.md`
- Modify: `CHANGELOG.md`
- Modify: `VERSION.txt`

**Interfaces:**
- No new public API; this task validates integration and records completion.

- [ ] **Step 1: Add integration tests** that load a profile at startup, apply it while a fake source is running, save it explicitly, restart the app store, and observe the same default snapshot and profile catalog.
- [ ] **Step 2: Run the full suite** with `dotnet test SystemAudioAnalyzer.sln`; require zero failed tests.
- [ ] **Step 3: Publish portable output** with `./release/rebuild.ps1`; verify `AAAnalyzer.exe`, LibVLC runtime, app-local `Data` startup behavior, and only `ru`/`en` locale directories.
- [ ] **Step 4: Perform Windows manual acceptance** against every reference settings screenshot, real audio peak bars and RMS visibility, live FFT reconfiguration during playback, profile restart persistence, and PNG export for every panel.
- [ ] **Step 5: Mark completed TODO items in `DONE.md`, retain the separate manual target-machine acceptance item if not performed, bump `VERSION.txt` to the next `a.bbb - yyyy.mm.dd` release, and describe the work in `CHANGELOG.md`.**
- [ ] **Step 6: Commit** the verified integration as `feat: complete full measurement settings`.
