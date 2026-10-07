# Full Measurement Settings and Presets

## Goal

Bring every settings page into functional alignment with the supplied Spectrum
Tool reference screenshots. All DSP, measurement, display, and color controls
must affect the running analyzer, and global profiles must survive application
restarts.

## Approved decisions

- Use one immutable application settings snapshot and a separate Core DSP
  configuration model. App/WPF settings never become a Core dependency.
- Apply changes live while audio is playing. The engine switches configurations
  only between analysis frames; changing FFT size recreates only the spectrum
  analyzer and clears its incompatible accumulation buffer. Capture, playback,
  and the source connection remain running.
- The Analyzer frequency-scale option controls Waterfall. Linear and logarithmic
  scales are both supported, with shared normalized cursor position for L/R.
- Profile catalog and settings data live in `Data` beside the executable. Logs
  and screenshots keep their existing locations.
- At startup the Default profile is loaded. The old
  `%LocalAppData%\AAAnalyzer\settings.json` is imported into a new Default
  profile on first use and is not deleted.
- Selecting a profile opens it as a draft. Apply/OK changes the current runtime
  snapshot only. Save overwrites that named profile; Save as creates a new
  profile; Default changes the profile loaded at next startup; Delete removes a
  non-default profile. Switching away from a dirty draft requires confirmation.
  Cancel restores the runtime snapshot that was active when the dialog opened.
- Color fields use the standard Windows color dialog. Gradient stops are sorted
  by dB, interpolated between neighboring stops, and persisted with the profile.

## Settings pages and behavior

### Analyzer

- FFT size: power-of-two choices from 512 through 16384 samples.
- Window: Rectangular, Hann, Hamming, or Blackman.
- Linear/logarithmic frequency scale; linear/logarithmic amplitude scale.
- Minimum display dB, analyzer gain, cursor color, and text color.
- New-install defaults follow the reference: 2048 samples, Blackman, linear
  frequency, logarithmic amplitude, -130 dB minimum, gain 1.

FFT/window changes are handled on the analysis worker at a frame boundary. The
new analyzer starts with an empty FFT accumulation window; no capture buffers
are synchronously drained or blocked from the UI thread.

### Waterfall

- Display floor and display offset in dB.
- Editable dB/color stops, with Add, Delete, and Color actions. Add creates a
  stop halfway between adjacent thresholds and interpolates its initial color;
  Delete is disabled when only two stops remain.
- Initial stops follow the reference thresholds (-110, -80, -55, -45 dB) and
  exact reference swatches: `#000000`, `#0080C0`, `#00FF39`, and `#E8E800`.
- Frequency mapping follows Analyzer's linear/log option. L and R use the same
  mapping, remain equal width, and share the normalized cursor.

### Meters

- Attack, release, and maximum-peak hold duration.
- Independent visibility controls for clip indicator, peak readout, RMS bars,
  dB scale, LUFS indicator, and LKFS readout.
- LUFS bar metric (Momentary, Short-term, or Integrated), rolling integrated
  duration, font size, and LUFS scale preset/range.
- Separate Peak, RMS, LUFS, and Clip colors.
- Peak bars use true-peak data; RMS bars use per-channel RMS from each analysis
  frame. Attack/release smooth display levels, peak-hold preserves the marker for
  its configured duration, and clip state remains independently resettable.

### Loudness

- History window, metric/mode, vertical span, and vertical center.
- Editable LUFS/color gradient stops, rendered against the visible history.
- Initial values and gradient thresholds follow the reference screenshot.
- Meter integrated-window duration is distinct from the Loudness history window.

### RTA

- Source, octave resolution, averaging count, tilt in dB/octave, and release in
  dB/second.
- Scale top/range, target line/range, peak-hold cap visibility and hold time.
- Bar, peak-cap, and target-band colors.
- Averaging, tilt, release, and peak hold affect the RTA data/rendering rather
  than only the settings form.

RTA averaging uses a bounded rolling average per frequency bin. Tilt is a
frequency-dependent display compensation relative to 1 kHz. Release controls
the decay rate of displayed band levels; peak hold is tracked separately and
expires after its configured duration. Reconfiguration resets only affected
bin histories and held peaks.

### Phase

- Gain compensation remains adjustable and persisted as today.

## Architecture and data flow

`MeasurementSettings` remains the immutable App/UI snapshot. A Core-owned
`AnalysisConfiguration` contains only DSP-relevant values, including FFT size
and window. `MainViewModel` applies the snapshot to renderers and maps its Core
subset through `AnalyzerController` to the engine.

The engine stores the latest requested configuration as an immutable value.
The processing loop observes it between sample buffers/analysis frames and
reconfigures its worker-owned analyzers there. No disk, WPF, modal dialog, or
long-lived lock is permitted on the audio-capture callback. Measurement/display
state that cannot remain mathematically valid after a setting change (FFT
history, incompatible RTA average bins, or rolling windows) is reset narrowly.

The preset catalog is a versioned JSON document under `<exe>/Data`, containing
named full snapshots and a Default profile identifier. Writes use a temporary
file and atomic replace. Validation rejects malformed ranges, colors, duplicate
or unordered gradient thresholds, and invalid enum values before save/apply.
Load failures log and fall back without preventing startup. The legacy settings
import is one-time and non-destructive.

The SettingsWindow mirrors the reference layout and common preset header across
pages. A color-stop list displays a swatch and dB/LUFS level; the selected stop
has an editable level and standard Windows color chooser. Peak and RMS bars are
visible according to their enablement controls and use the configured colors.

## Defaults and migration

New Default profile values follow reference screenshot values where shown.
Existing settings are mapped field-by-field into that schema so current
Waterfall floor/offset/color, meter range/colors, loudness window/mode/scale,
RTA source/resolution/scales/target, and Phase gain are preserved. New fields
receive their reference defaults. The old file remains available for recovery.

## Verification and acceptance

- Core tests cover FFT-size/window changes, window functions, output metadata,
  and live reconfiguration without restarting the source.
- App tests cover settings validation, gradient interpolation and editing,
  meter-level geometry/visibility/ballistics, RTA averaging/tilt/release/hold,
  loudness gradient and rolling integration, preset save/load/delete/default,
  dirty-draft cancellation, and legacy migration.
- Build and portable publish verify the app-local `Data` directory behavior and
  allowed `ru`/`en` localization resources.
- Manual Windows acceptance verifies the reference dialog layout, color chooser,
  all six pages, audible live reconfiguration, level bars, preset startup
  selection, and restart persistence.
