# Tabbed Workspace and Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a tabbed full-workspace measurement UI with persistent, transactional configuration for every instrument.

**Architecture:** Keep persistent settings and edit-session logic in small App-layer classes independent of WPF. `MainViewModel` owns the immutable applied snapshot; custom WPF controls receive it through dependency properties and redraw only on the dispatcher. A common settings dialog edits a copy and commits it through `SettingsStore`.

**Tech Stack:** .NET 8, WPF, System.Text.Json, xUnit; no new packages.

**Spec:** `docs/superpowers/specs/2026-10-07-tabbed-measurement-workspace-design.md`

## Global Constraints

- Keep `SystemAudioAnalyzer.Core` at `net8.0`; settings and WPF controls remain in `SystemAudioAnalyzer.App` at `net8.0-windows`.
- Store JSON at `%LocalAppData%\AAAnalyzer\settings.json`; invalid, missing or unreadable data falls back to defaults and is logged.
- Apply settings atomically through a temporary file; no setting write may block an audio callback.
- Waterfall always presents equal-width side-by-side L/R areas, using the shared 20 Hz–20 kHz logarithmic scale.
- Every instrument tab exposes Reset, Screenshot and Settings at the same header position.
- Preserve `VERSION.txt` format `a.bbb - yyyy.mm.dd`; increment `bbb`, update `CHANGELOG.md`, `TODO.md` and `DONE.md` for every commit.
- Before every commit, inspect uncommitted user changes and ask whether they belong in that commit.

## Review Focus

- Corrupt or schema-incompatible settings must start with defaults rather than stop the GUI; Task 1 tests this fallback.
- Cancel must restore every unsaved dialog field and not write a JSON file; Task 2 tests the edit session rollback.
- Narrow windows must leave the 130 px rail intact and not distort the active tab; Task 3 tests layout dimensions.
- A Waterfall pointer over either L or R must map to identical normalized frequency; Task 4 preserves the existing geometry test and adds right-pane input coverage.
- Screenshot failure must not change source/analysis state; Task 5 preserves the existing error-path test when wiring toolbars.

---

## Planned File Structure

```text
src/SystemAudioAnalyzer.App/
  Settings/
    AnalyzerSettings.cs
    MeasurementSettings.cs
    SettingsStore.cs
    SettingsEditSession.cs
  ViewModels/
    MainViewModel.cs
    InstrumentTab.cs
    SettingsDialogViewModel.cs
  Views/
    SettingsWindow.xaml
    SettingsWindow.xaml.cs
  Rendering/
    MeterRailView.cs
    WaterfallView.cs
    LoudnessView.cs
    RtaView.cs
    PhaseScopeView.cs
  MainWindow.xaml
  MainWindow.xaml.cs
tests/SystemAudioAnalyzer.App.Tests/
  SettingsStoreTests.cs
  SettingsEditSessionTests.cs
  WorkspaceLayoutTests.cs
  WaterfallLayoutTests.cs
```

### Task 1: Add immutable settings and persistence

**Files:**
- Create: `src/SystemAudioAnalyzer.App/Settings/MeasurementSettings.cs`
- Create: `src/SystemAudioAnalyzer.App/Settings/SettingsStore.cs`
- Test: `tests/SystemAudioAnalyzer.App.Tests/SettingsStoreTests.cs`

**Interfaces:**
- Produces `MeasurementSettings.Default` with nested immutable page settings for Analyzer, Waterfall, Meters, Loudness, RTA and Phase.
- Produces `Task<MeasurementSettings> SettingsStore.LoadAsync(CancellationToken)` and `Task SaveAsync(MeasurementSettings, CancellationToken)`.

- [ ] **Step 1: Write failing persistence tests**

```csharp
[Fact]
public async Task RoundTripPreservesWaterfallFloorAndRtaResolution() { }

[Fact]
public async Task InvalidJsonReturnsDefaults() { }
```

- [ ] **Step 2: Run focused tests to verify they fail**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~SettingsStoreTests`

Expected: FAIL because settings types do not exist.

- [ ] **Step 3: Implement immutable settings and `SettingsStore`**

Use `System.Text.Json`; write `settings.json.tmp`, then replace/move it into `%LocalAppData%\AAAnalyzer\settings.json`. Return `MeasurementSettings.Default` for parse/read errors and expose a non-throwing diagnostic callback for the caller to log.

- [ ] **Step 4: Run focused tests to verify they pass**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~SettingsStoreTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/SystemAudioAnalyzer.App/Settings tests/SystemAudioAnalyzer.App.Tests/SettingsStoreTests.cs VERSION.txt CHANGELOG.md TODO.md DONE.md
git commit -m "feat: persist measurement settings"
```

### Task 2: Add transactional settings editing and dialog view model

**Files:**
- Create: `src/SystemAudioAnalyzer.App/Settings/SettingsEditSession.cs`
- Create: `src/SystemAudioAnalyzer.App/ViewModels/InstrumentTab.cs`
- Create: `src/SystemAudioAnalyzer.App/ViewModels/SettingsDialogViewModel.cs`
- Test: `tests/SystemAudioAnalyzer.App.Tests/SettingsEditSessionTests.cs`

**Interfaces:**
- Consumes `MeasurementSettings`.
- Produces `SettingsEditSession.Current`, `Apply()`, `Cancel()` and `SelectPage(InstrumentTab)`.
- Produces `SettingsDialogViewModel.SelectedPage` and Apply/OK/Cancel commands.

- [ ] **Step 1: Write failing edit-session tests**

```csharp
[Fact]
public void CancelRestoresTheOpeningSettingsSnapshot() { }

[Fact]
public void ApplyPublishesTheEditedSnapshot() { }
```

- [ ] **Step 2: Run focused tests to verify they fail**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~SettingsEditSessionTests`

Expected: FAIL because edit session types do not exist.

- [ ] **Step 3: Implement the edit session and dialog view model**

Expose editable copies for all six pages. Keep Analyzer FFT/window controls visibly disabled because the Core interfaces do not yet accept runtime DSP configuration. Apply invokes `SettingsStore.SaveAsync`; Cancel restores the opening immutable snapshot without writing.

- [ ] **Step 4: Run focused tests to verify they pass**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~SettingsEditSessionTests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/SystemAudioAnalyzer.App/Settings src/SystemAudioAnalyzer.App/ViewModels tests/SystemAudioAnalyzer.App.Tests VERSION.txt CHANGELOG.md TODO.md DONE.md
git commit -m "feat: add transactional measurement settings"
```

### Task 3: Replace dashboard with tabbed workspace and common actions

**Files:**
- Create: `src/SystemAudioAnalyzer.App/Views/SettingsWindow.xaml`
- Create: `src/SystemAudioAnalyzer.App/Views/SettingsWindow.xaml.cs`
- Modify: `src/SystemAudioAnalyzer.App/MainWindow.xaml`
- Modify: `src/SystemAudioAnalyzer.App/MainWindow.xaml.cs`
- Modify: `src/SystemAudioAnalyzer.App/ViewModels/MainViewModel.cs`
- Test: `tests/SystemAudioAnalyzer.App.Tests/WorkspaceLayoutTests.cs`

**Interfaces:**
- Consumes `InstrumentTab`, `MeasurementSettings`, `IScreenshotService` and existing reset controller APIs.
- Produces `MainViewModel.ActiveTab`, `SelectTab(InstrumentTab)` and `OpenSettings(InstrumentTab)`.

- [ ] **Step 1: Write failing workspace tests**

```csharp
[Fact]
public void WorkspaceKeepsThePeakRailAtOneHundredThirtyPixels() { }

[Fact]
public void SelectingAnInstrumentMakesOnlyThatTabActive() { }
```

- [ ] **Step 2: Run focused tests to verify they fail**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter FullyQualifiedName~WorkspaceLayoutTests`

Expected: FAIL because tab state and layout constants do not exist.

- [ ] **Step 3: Implement tabbed `MainWindow` and common toolbar**

Use a 130 px left column, a WPF `TabControl` for the four full-area instruments, and the same header template ordering `Reset`, `Screenshot`, `Settings` for each tab. Settings opens `SettingsWindow` with the active page preselected. Continue using the existing screenshot and reset services.

- [ ] **Step 4: Run App tests and build**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests; dotnet build SystemAudioAnalyzer.sln`

Expected: PASS with no warnings.

- [ ] **Step 5: Commit**

```powershell
git add src/SystemAudioAnalyzer.App tests/SystemAudioAnalyzer.App.Tests VERSION.txt CHANGELOG.md TODO.md DONE.md
git commit -m "feat: add tabbed measurement workspace"
```

### Task 4: Apply settings to instrument renderers and refine visual scales

**Files:**
- Modify: `src/SystemAudioAnalyzer.App/Rendering/MeterRailView.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/WaterfallView.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/LoudnessView.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/RtaView.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/PhaseScopeView.cs`
- Modify: `src/SystemAudioAnalyzer.App/Rendering/WaterfallLayout.cs`
- Test: `tests/SystemAudioAnalyzer.App.Tests/WaterfallLayoutTests.cs`
- Test: `tests/SystemAudioAnalyzer.App.Tests/RtaRenderingTests.cs`

**Interfaces:**
- Consumes the applied `MeasurementSettings` snapshot via renderer dependency properties.
- Produces a `MeasurementSettings` dependency property on each renderer.

- [ ] **Step 1: Write failing renderer-geometry tests**

```csharp
[Fact]
public void CursorRelativePositionIsEqualWhenEnteredOverEitherStereoPane() { }

[Fact]
public void MeterRailReservesMaximumCurrentAndOverloadRowsBeforeBars() { }
```

- [ ] **Step 2: Run focused tests to verify they fail**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter "FullyQualifiedName~WaterfallLayoutTests|FullyQualifiedName~RtaRenderingTests"`

Expected: FAIL because settings-aware geometry does not exist.

- [ ] **Step 3: Implement settings-aware rendering**

Map Waterfall bins logarithmically, draw a visible L/R separator and non-overlapping ticks. Add RTA grid/target line and active-state styling, loudness timestamp and LUFS labels with ten-second/one-LUFS grid, a square phase viewport, and resettable maximum/overload hit regions in the peak rail.

- [ ] **Step 4: Run App rendering tests**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests`

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add src/SystemAudioAnalyzer.App/Rendering tests/SystemAudioAnalyzer.App.Tests VERSION.txt CHANGELOG.md TODO.md DONE.md
git commit -m "feat: refine measurement instrument rendering"
```

### Task 5: Wire startup, document settings and release-check the GUI

**Files:**
- Modify: `src/SystemAudioAnalyzer.App/MainWindow.xaml.cs`
- Modify: `README.md`
- Modify: `TODO.md`
- Modify: `DONE.md`
- Test: `tests/SystemAudioAnalyzer.App.Tests/StartupAndLoggingTests.cs`
- Test: `tests/SystemAudioAnalyzer.App.Tests/ScreenshotServiceTests.cs`

**Interfaces:**
- Consumes `SettingsStore.LoadAsync`, `MeasurementSettings`, `SettingsWindow` and existing logging/screenshot services.
- Produces a startup path that loads settings before display and logs a fallback reason without closing the application.

- [ ] **Step 1: Write failing startup and screenshot regression tests**

```csharp
[Fact]
public async Task InvalidSavedSettingsDoNotPreventWindowStartup() { }

[Fact]
public async Task ScreenshotFailureLeavesTheSelectedInstrumentActive() { }
```

- [ ] **Step 2: Run focused tests to verify they fail**

Run: `dotnet test tests/SystemAudioAnalyzer.App.Tests --filter "FullyQualifiedName~StartupAndLoggingTests|FullyQualifiedName~ScreenshotServiceTests"`

Expected: FAIL because startup is not settings-aware.

- [ ] **Step 3: Implement startup wiring and documentation**

Load the stored snapshot before constructing/binding the main view model; route fallback diagnostics through `AppLogger`. Describe settings location, tab workflow and screenshots in `README.md`. Move only manually verified GUI requirements from TODO to DONE.

- [ ] **Step 4: Run the full verification matrix and manual acceptance checks**

Run: `dotnet test SystemAudioAnalyzer.sln; dotnet build SystemAudioAnalyzer.sln; .\release\rebuild.ps1`

Expected: all tests pass; portable output includes `AAAnalyzer.exe`, expected LibVLC assets and only `ru`/`en` localisation resources.

Manual checks: launch using a local WASAPI device, verify every tab fills its whole workspace, resize Waterfall, click maximum/overload resets, change a setting then relaunch, save screenshots from every tab, and verify invalid `settings.json` falls back while writing a log entry.

- [ ] **Step 5: Commit**

```powershell
git add src/SystemAudioAnalyzer.App tests/SystemAudioAnalyzer.App.Tests README.md TODO.md DONE.md VERSION.txt CHANGELOG.md
git commit -m "feat: complete configurable tabbed workspace"
```
