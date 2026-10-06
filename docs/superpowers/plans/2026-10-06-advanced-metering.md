# Advanced Metering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver EBU R128 loudness, 4x stereo true-peak, configurable RTA, cursor-driven Waterfall, and phase-scope instruments in AAAnalyzer.

**Architecture:** Extend Core with immutable advanced-measurement values generated in the existing bounded audio-analysis pipeline. Keep history and geometry in small testable App classes; WPF controls only redraw immutable snapshots on the Dispatcher. Each pane owns compact presentation history and reset/screenshot commands, while Core owns audio-derived accumulators.

**Tech Stack:** .NET 8, WPF, NAudio, LibVLCSharp, xUnit; no new UI or DSP packages.

**Spec:** `docs/superpowers/specs/2026-10-06-advanced-metering-design.md`

## Global Constraints

- Keep `SystemAudioAnalyzer.Core` at `net8.0`, with no WPF or LibVLC dependency; WPF remains `net8.0-windows`.
- Implement stereo EBU R128: K-weighting, 400 ms Momentary, 3 s Short-term, and absolute/relative gated Integrated loudness.
- True Peak uses 4x oversampling and latches overload at linear 1.0 (0 dBFS).
- Limit Waterfall to 10 seconds and Loudness history to 60 seconds; stale visual data must be discarded.
- Waterfall L/R rectangles must remain equal width at every size and share one logarithmic cursor/frequency mapping from 20 Hz to 20 kHz.
- Screenshots are PNG files in `Screenshots` next to the executable; failures go to `AAAnalyzer.log`.
- Preserve `VERSION.txt` format `a.bbb - yyyy.mm.dd`; increment `bbb`, update `CHANGELOG.md`, `TODO.md` and `DONE.md` for each commit.
- Before every commit, inspect uncommitted user changes and ask whether they belong in that commit.

## Review Focus

- Silence and short inputs must not yield NaN/Infinity LUFS or true-peak values; Task 1 and 2 unit tests cover empty/silent buffers.
- A left-only or right-only signal must not cause cross-channel overload or phase data; Task 1 and 3 test channel isolation.
- Integrated loudness must not change after a pane-only reset; Task 2 tests Core reset scope separately from UI history reset.
- Narrow, wide and resized Waterfall layouts must preserve equal L/R widths, cursor position and frequency label; Task 4 tests all three.
- Screenshot failure (unwritable directory) must preserve the running analyser and write a diagnostic; Task 6 tests the error path through an injected file writer.

---

## Planned file structure

```text
src/
  SystemAudioAnalyzer.Core/
    TruePeakMeter.cs
    LoudnessMeter.cs
    LoudnessMeasurement.cs
    StereoSpectrum.cs
    PhaseScopeFrame.cs
    AdvancedMeasurementFrame.cs
    AnalysisFrame.cs                 # extended immutable frame
    AudioAnalysisEngine.cs           # produces/reset advanced measurements
  SystemAudioAnalyzer.App/
    Rendering/
      FrequencyScale.cs
      WaterfallHistory.cs
      LoudnessHistory.cs
      RtaBandAggregator.cs
      PhaseScopeTransform.cs
      MeterRailView.cs
      WaterfallView.cs
      LoudnessView.cs
      RtaView.cs
      PhaseScopeView.cs
    Services/
      IScreenshotService.cs
      ScreenshotService.cs
    ViewModels/
      MainViewModel.cs
      RtaChannelMode.cs
      RtaResolution.cs
tests/
  SystemAudioAnalyzer.Core.Tests/
    TruePeakMeterTests.cs
    LoudnessMeterTests.cs
    AdvancedMeasurementEngineTests.cs
  SystemAudioAnalyzer.App.Tests/
    FrequencyScaleTests.cs
    WaterfallHistoryTests.cs
    LoudnessHistoryTests.cs
    RtaBandAggregatorTests.cs
    ScreenshotServiceTests.cs
```

### Task 1: Add true-peak and immutable advanced measurement contracts

**Files:**
- Create: `src/SystemAudioAnalyzer.Core/TruePeakMeter.cs`
- Create: `src/SystemAudioAnalyzer.Core/StereoTruePeakMeasurement.cs`
- Create: `src/SystemAudioAnalyzer.Core/AdvancedMeasurementFrame.cs`
- Modify: `src/SystemAudioAnalyzer.Core/AnalysisFrame.cs`
- Modify: `src/SystemAudioAnalyzer.Core/AudioAnalysisEngine.cs`
- Test: `tests/SystemAudioAnalyzer.Core.Tests/TruePeakMeterTests.cs`
- Test: `tests/SystemAudioAnalyzer.Core.Tests/AdvancedMeasurementEngineTests.cs`

**Interfaces:**
- Produces `StereoTruePeakMeasurement(IReadOnlyList<float> Current, IReadOnlyList<float> Maximum, IReadOnlyList<bool> Overload)`.
- Produces `TruePeakMeter.Process(ReadOnlySpan<float> interleaved, int channels)` and `TruePeakMeter.Reset(int channel)`.
- Produces `AnalysisFrame.AdvancedMeasurements` and `AudioAnalysisEngine.ResetTruePeak(int channel)`.
- Consumes the interleaved `float` PCM supplied by the existing processing loop.

- [ ] **Step 1: Write failing true-peak tests**

```csharp
[Fact]
public void FourTimesOversamplingFindsAnInterSamplePeak() { }

[Fact]
public void OverloadLatchesOnlyForTheAffectedChannelUntilReset() { }
```

- [ ] **Step 2: Run Core true-peak tests to verify they fail**

Run: `dotnet test tests/SystemAudioAnalyzer.Core.Tests --filter FullyQualifiedName~TruePeakMeterTests`

Expected: FAIL because true-peak types do not exist.

- [ ] **Step 3: Implement the 4x true-peak meter and frame contract**

Use a deterministic 4x polyphase/interpolation implementation with per-channel state across buffers. Compute current and session maximums in linear amplitude; set overload when amplitude is at least `1.0`. Extend `AnalysisFrame` without mutating existing level/spectrum collections.

- [ ] **Step 4: Write and run engine integration tests**

Assert a synthetic stereo source publishes `AdvancedMeasurements`, `ResetTruePeak(0)` clears only left maximum/latch, and old peak/RMS tests still pass.

- [ ] **Step 5: Commit**

```powershell
git add src/SystemAudioAnalyzer.Core tests/SystemAudioAnalyzer.Core.Tests VERSION.txt CHANGELOG.md TODO.md DONE.md
git commit -m "feat: add true peak measurements"
```

### Task 2: Implement EBU R128 loudness measurement

**Files:**
- Create: `src/SystemAudioAnalyzer.Core/LoudnessMeasurement.cs`
- Create: `src/SystemAudioAnalyzer.Core/LoudnessMeter.cs`
- Modify: `src/SystemAudioAnalyzer.Core/AdvancedMeasurementFrame.cs`
- Modify: `src/SystemAudioAnalyzer.Core/AudioAnalysisEngine.cs`
- Test: `tests/SystemAudioAnalyzer.Core.Tests/LoudnessMeterTests.cs`
- Test: `tests/SystemAudioAnalyzer.Core.Tests/AdvancedMeasurementEngineTests.cs`

**Interfaces:**
- Produces `LoudnessMeasurement(float? MomentaryLufs, float? ShortTermLufs, float? IntegratedLufs)`.
- Produces `LoudnessMeter.Process(ReadOnlySpan<float> interleaved, AudioFormat format)` and `Reset()`.
- Produces `AudioAnalysisEngine.ResetLoudness()`; it does not reset true-peak state.

- [ ] **Step 1: Write failing loudness tests**

```csharp
[Fact]
public void SilenceProducesNoFiniteLoudnessValues() { }

[Fact]
public void ShortTermValueAppearsOnlyAfterThreeSeconds() { }

[Fact]
public void ResetClearsIntegratedLoudnessWithoutChangingTruePeak() { }
```

- [ ] **Step 2: Run loudness tests to verify they fail**

Run: `dotnet test tests/SystemAudioAnalyzer.Core.Tests --filter FullyQualifiedName~LoudnessMeterTests`

Expected: FAIL because the loudness meter does not exist.

- [ ] **Step 3: Implement K-weighting, windows and gates**

Use per-channel K-weighting filters. Calculate Momentary from a 400 ms rolling window and Short-term from a 3 s window. For Integrated, retain block energies and apply the EBU R128 absolute gate then the relative gate; return `null` until a valid gated value exists.

- [ ] **Step 4: Run Core tests**

Run: `dotnet test tests/SystemAudioAnalyzer.Core.Tests`

Expected: PASS, including silence, timing and reset-scope tests.

- [ ] **Step 5: Commit**

```powershell
git add src/SystemAudioAnalyzer.Core tests/SystemAudioAnalyzer.Core.Tests VERSION.txt CHANGELOG.md TODO.md DONE.md
git commit -m "feat: add EBU R128 loudness measurements"
```

### Task 3: Publish stereo spectra and phase-scope data

**Files:**
- Create: `src/SystemAudioAnalyzer.Core/StereoSpectrum.cs`
- Create: `src/SystemAudioAnalyzer.Core/PhaseScopeFrame.cs`
- Modify: `src/SystemAudioAnalyzer.Core/SpectrumAnalyzer.cs`
- Modify: `src/SystemAudioAnalyzer.Core/AdvancedMeasurementFrame.cs`
- Modify: `src/SystemAudioAnalyzer.Core/AudioAnalysisEngine.cs`
- Test: `tests/SystemAudioAnalyzer.Core.Tests/SpectrumAnalyzerTests.cs`
- Test: `tests/SystemAudioAnalyzer.Core.Tests/AdvancedMeasurementEngineTests.cs`

**Interfaces:**
- Produces `StereoSpectrum(Spectrum Mono, Spectrum Left, Spectrum Right)`.
- Produces `PhaseScopeFrame(IReadOnlyList<(float Left, float Right)> Points)` with a fixed maximum point count.
- Extends the spectrum analyser with `TryProcessStereo(...)`; preserve the existing `TryProcess(...)` API for existing consumers.

- [ ] **Step 1: Write failing channel-isolation and phase-frame tests**

```csharp
[Fact]
public void LeftOnlyToneAppearsOnlyInLeftSpectrum() { }

[Fact]
public void PhaseFrameIsBoundedAndPreservesStereoPairs() { }
```

- [ ] **Step 2: Run the focused tests to verify they fail**

Run: `dotnet test tests/SystemAudioAnalyzer.Core.Tests --filter FullyQualifiedName~Stereo`

Expected: FAIL because stereo spectrum and phase types do not exist.

- [ ] **Step 3: Implement shared-window stereo FFT and decimated phase samples**

Run three FFT accumulators over the same cadence: mono mix, left, and right. Extract evenly decimated paired samples from the latest buffer, capped at the fixed phase-point count, and publish only immutable arrays.

- [ ] **Step 4: Run Core suite**

Run: `dotnet test tests/SystemAudioAnalyzer.Core.Tests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/SystemAudioAnalyzer.Core tests/SystemAudioAnalyzer.Core.Tests VERSION.txt CHANGELOG.md TODO.md DONE.md
git commit -m "feat: publish stereo spectrum and phase data"
```

### Task 4: Build shared visual scales and bounded histories

**Files:**
- Create: `src/SystemAudioAnalyzer.App/Rendering/FrequencyScale.cs`
- Create: `src/SystemAudioAnalyzer.App/Rendering/WaterfallHistory.cs`
- Create: `src/SystemAudioAnalyzer.App/Rendering/LoudnessHistory.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/WaterfallLayout.cs`
- Test: `tests/SystemAudioAnalyzer.App.Tests/FrequencyScaleTests.cs`
- Test: `tests/SystemAudioAnalyzer.App.Tests/WaterfallHistoryTests.cs`
- Test: `tests/SystemAudioAnalyzer.App.Tests/LoudnessHistoryTests.cs`

**Interfaces:**
- Produces `FrequencyScale.ToNormalized(double hertz)`, `ToHertz(double normalized)`, and `Format(double hertz)`.
- Produces `WaterfallHistory.Append(DateTimeOffset, StereoSpectrum)` and `GetVisibleRows(DateTimeOffset)` with a 10-second retention limit.
- Produces `LoudnessHistory.Append(DateTimeOffset, LoudnessMeasurement)` and `GetVisiblePoints(DateTimeOffset)` with a 60-second limit.

- [ ] **Step 1: Write failing scale and history tests**

```csharp
[Theory]
[InlineData(999, "999 Hz")]
[InlineData(1_000, "1.000 kHz")]
[InlineData(10_000, "10.0 kHz")]
public void FrequencyScaleFormatsCursorLabels(double hertz, string expected) { }

[Fact]
public void WaterfallHistoryTrimsRowsOlderThanTenSeconds() { }

[Fact]
public void LoudnessHistoryTrimsPointsOlderThanOneMinute() { }
```

- [ ] **Step 2: Run App rendering tests to verify they fail**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~FrequencyScaleTests`

Expected: FAIL because the scale and histories do not exist.

- [ ] **Step 3: Implement normalised log-frequency mapping and time-bounded histories**

Use `20 Hz` and `20_000 Hz` as the shared mapping endpoints. Store normalized spectrum rows, not bitmaps; keep timestamped loudness values and calculate display range with a stable one-LUFS margin.

- [ ] **Step 4: Run App test suite**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests`

Expected: PASS, including existing equal-width Waterfall layout tests.

- [ ] **Step 5: Commit**

```powershell
git add src/SystemAudioAnalyzer.App tests/SystemAudioAnalyzer.App.Tests VERSION.txt CHANGELOG.md TODO.md DONE.md
git commit -m "feat: add visual measurement histories"
```

### Task 5: Render interactive measurement panes and controls

**Files:**
- Create: `src/SystemAudioAnalyzer.App/Rendering/MeterRailView.cs`
- Create: `src/SystemAudioAnalyzer.App/Rendering/WaterfallView.cs`
- Create: `src/SystemAudioAnalyzer.App/Rendering/LoudnessView.cs`
- Create: `src/SystemAudioAnalyzer.App/Rendering/RtaBandAggregator.cs`
- Create: `src/SystemAudioAnalyzer.App/Rendering/PhaseScopeTransform.cs`
- Create: `src/SystemAudioAnalyzer.App/Rendering/RtaView.cs`
- Create: `src/SystemAudioAnalyzer.App/Rendering/PhaseScopeView.cs`
- Create: `src/SystemAudioAnalyzer.App/ViewModels/RtaChannelMode.cs`
- Create: `src/SystemAudioAnalyzer.App/ViewModels/RtaResolution.cs`
- Modify: `src/SystemAudioAnalyzer.App/ViewModels/MainViewModel.cs`
- Modify: `src/SystemAudioAnalyzer.App/MainWindow.xaml`
- Test: `tests/SystemAudioAnalyzer.App.Tests/RtaBandAggregatorTests.cs`
- Test: `tests/SystemAudioAnalyzer.App.Tests/WaterfallLayoutTests.cs`

**Interfaces:**
- Consumes `AnalysisFrame.AdvancedMeasurements`, `FrequencyScale`, `WaterfallHistory`, and `LoudnessHistory`.
- Produces dependency properties for `AnalysisFrame`, `RtaChannelMode`, `RtaResolution`, and panel-local reset commands.
- Produces `RtaBandAggregator.Aggregate(Spectrum spectrum, RtaResolution resolution)`.
- Produces `PhaseScopeTransform.Transform(float left, float right, float gain) -> Point` for the rotated display convention.

- [ ] **Step 1: Write failing RTA, cursor and phase geometry tests**

```csharp
[Fact]
public void CursorNormalisedPositionMapsToTheSameXInBothWaterfallPanels() { }

[Theory]
[InlineData(RtaResolution.OneThird, 3)]
[InlineData(RtaResolution.OneTwelfth, 12)]
public void RtaAggregatorCreatesRequestedBandsPerOctave(RtaResolution resolution, int bands) { }

[Fact]
public void PhaseRotationMapsMonoToVerticalAndAntiphaseToHorizontal() { }
```

- [ ] **Step 2: Run focused App tests to verify they fail**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~RtaBandAggregatorTests`

Expected: FAIL because interactive pane implementations do not exist.

- [ ] **Step 3: Implement drawing controls and bind them in the main window**

Replace the combined `RealtimeAnalyzerView` with separate panes and controls. Use mouse position with `FrequencyScale` for one shared white Waterfall cursor; draw labels/scales from live `RenderSize`. Meter clicks call narrowly scoped reset methods. RTA controls alter aggregation/display only. Phase compensation changes the view transform only.

- [ ] **Step 4: Run App tests and build**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests; dotnet build SystemAudioAnalyzer.sln`

Expected: PASS with no warnings.

- [ ] **Step 5: Commit**

```powershell
git add src/SystemAudioAnalyzer.App tests/SystemAudioAnalyzer.App.Tests VERSION.txt CHANGELOG.md TODO.md DONE.md
git commit -m "feat: render advanced measurement panes"
```

### Task 6: Add reset orchestration, screenshots and acceptance documentation

**Files:**
- Create: `src/SystemAudioAnalyzer.App/Services/IScreenshotService.cs`
- Create: `src/SystemAudioAnalyzer.App/Services/ScreenshotService.cs`
- Modify: `src/SystemAudioAnalyzer.App/Services/IAnalyzerController.cs`
- Modify: `src/SystemAudioAnalyzer.App/Services/AnalyzerController.cs`
- Modify: `src/SystemAudioAnalyzer.App/ViewModels/MainViewModel.cs`
- Modify: `src/SystemAudioAnalyzer.App/MainWindow.xaml`
- Modify: `README.md`
- Modify: `TODO.md`
- Modify: `DONE.md`
- Test: `tests/SystemAudioAnalyzer.App.Tests/ScreenshotServiceTests.cs`
- Test: `tests/SystemAudioAnalyzer.App.Tests/AnalyzerControllerTests.cs`

**Interfaces:**
- Produces `Task<string> IScreenshotService.SaveAsync(FrameworkElement element, string paneName, CancellationToken)`.
- Extends `IAnalyzerController` with `ResetTruePeak(int channel)`, `ResetLoudness()` and `ResetAllMeasurements()`.
- `MainViewModel` produces commands for each pane's reset and screenshot action, forwarding failures to `StatusText` and `AppLogger`.

- [ ] **Step 1: Write failing reset and screenshot tests**

```csharp
[Fact]
public async Task ScreenshotUsesPaneNameAndTimestampUnderScreenshotsDirectory() { }

[Fact]
public async Task ScreenshotFailureDoesNotStopTheController() { }

[Fact]
public async Task LoudnessResetDoesNotResetTruePeakMeasurements() { }
```

- [ ] **Step 2: Run focused tests to verify they fail**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~ScreenshotServiceTests`

Expected: FAIL because screenshot and reset APIs do not exist.

- [ ] **Step 3: Implement screenshot capture and reset forwarding**

Render each pane with `RenderTargetBitmap`, encode PNG, and write with an injected filesystem abstraction so error handling is testable. Create `Screenshots` lazily next to `AppContext.BaseDirectory`; log failures while leaving measurement and source state running. Wire controller reset calls to the engine and pane-history clearing.

- [ ] **Step 4: Run full verification matrix**

Run: `dotnet test SystemAudioAnalyzer.sln; dotnet build SystemAudioAnalyzer.sln; .\release\rebuild.ps1`

Expected: all tests and build pass; release contains `AAAnalyzer.exe`, LibVLC native assets, and only `ru` locale resources.

- [ ] **Step 5: Perform and record manual acceptance checks**

Check local WASAPI and reachable Icecast/HLS sources; verify all visual panes update, Waterfall cursor synchronization/resize, overload and maximum reset scopes, screenshot creation, and loudness/true-peak reference signals. Record known source limitations in `README.md`; then move completed GUI tasks from `TODO.md` to `DONE.md`.

- [ ] **Step 6: Commit**

```powershell
git add -A
git commit -m "feat: complete advanced audio metering"
```
