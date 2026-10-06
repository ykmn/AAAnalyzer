# Advanced Metering and Visualisation Design

## Goal

Extend AAAnalyzer into a real-time stereo measurement application with EBU R128 loudness, 4x true-peak metering, a cursor-driven stereo Waterfall, configurable RTA, and a phase scope.

## Scope

Included:

- stereo true peak and persistent maximum true peak, with overload latches and independent reset actions;
- EBU R128 Momentary (400 ms), Short-term (3 s), and gated Integrated loudness;
- Waterfall with 10 seconds of history, matched L/R cursor positions, frequency readout and 20 Hz–20 kHz scale;
- RTA channel selection (mono/L/R) and 1/1, 1/3, 1/6, 1/12 octave aggregation;
- a rotated stereo phase scope with gain compensation;
- reset and PNG screenshot controls for each visual pane.

Excluded:

- recording, per-process capture, calibration against an external meter, export of measurements, or automatic source retry;
- claims of EBU R128 compliance for nonstandard channel layouts beyond mono and stereo.

## Core measurement model

`AudioAnalysisEngine` remains the sole consumer of PCM. It produces an immutable `AnalysisFrame` that contains existing peak/RMS values plus an `AdvancedMeasurementFrame`:

- `StereoTruePeakMeasurement`: current and session-maximum 4x true-peak values for L/R, and L/R overload latches. The overload threshold is 0 dBFS (linear value `1.0`).
- `LoudnessMeasurement`: Momentary, Short-term and gated Integrated values in LUFS. It uses K-weighting, 400 ms and 3 s rolling windows, plus the EBU R128 absolute and relative gates for Integrated loudness.
- `StereoSpectra`: immutable mono, left and right spectra emitted from the same analysis interval. It replaces the current mono-only visual input without changing the source abstraction.
- `PhaseScopeFrame`: a bounded, decimated collection of current stereo sample pairs; rendering rotates the conventional X/Y display so mono forms a vertical line and antiphase a horizontal line.

The engine exposes `ResetTruePeak(int channel)`, `ResetLoudness()` and `ResetMeasurements()`; the first two preserve unrelated measurements, while the last clears every engine accumulator. The existing bounded PCM queue remains the latency boundary; visual history never feeds back into audio processing.

## UI and interaction model

The main window becomes an instrument workspace with a source/control strip and independent panes:

- **Meter rail**: L/R vertical true-peak bars, red overload latches, current peak text and maximum peak text. Clicking the latch resets its overload state; clicking a maximum resets only that maximum.
- **Waterfall**: two equal-width L/R panels sharing one normalised frequency position. Mouse movement over either panel displays a white vertical cursor in both panels and a top frequency label: `000 Hz` below 1 kHz, `0.000 kHz` from 1–10 kHz, and `00.0 kHz` above 10 kHz. Ten seconds fit vertically and the bottom scale spans 20 Hz–20 kHz.
- **Loudness**: a 60-second scrolling plot of Momentary and Short-term loudness plus an Integrated numeric readout. Time labels use `hh:mm:ss`; grid spacing is 10 seconds horizontally and 1 LUFS vertically. The vertical range follows the visible data with a stable margin to avoid jitter.
- **RTA**: buttons select octave precision and mono/L/R input. Renderer groups current spectrum bins by logarithmic octave bands before drawing.
- **Phase scope**: square visual with gain compensation knob. The gain affects display only, not measurements or audio.

Every pane has **Reset** and **Screenshot**. Reset clears that pane's presentation history; when it corresponds to an engine accumulator, it calls the narrowly scoped engine reset operation. Screenshot writes PNG to `Screenshots` beside the executable, naming the file with pane and timestamp. A failed screenshot reports a status message and is written to `AAAnalyzer.log`.

## Rendering and threading

Rendering remains Dispatcher-owned. ViewModels receive immutable frame snapshots, coalesce frequent frames into one pending UI update, and append only compact normalised rows/points to pane histories. All geometry derives from current `RenderSize`:

- Waterfall redraws history within recalculated equal-width L/R rectangles at every resize;
- loudness and phase scope redraw vectors rather than scaling old pixels;
- frequency hit testing uses the same logarithmic mapping as RTA and Waterfall labels.

## Testing and acceptance

Automated tests will use synthetic PCM to validate:

- per-channel 4x true peak, overload latching and each reset scope;
- loudness window durations, silence behaviour and integrated-loudness gating;
- L/R Waterfall geometry, frequency mapping and label formatting at each threshold;
- 10-second and 60-second history trimming, RTA band grouping and channel selection;
- phase rotation and display-only gain compensation;
- screenshot target name/path generation without requiring a visible UI.

Manual acceptance covers local WASAPI playback and both stream source types, all panel reset/screenshot actions, narrow/wide resize, cursor synchronisation, and comparison of loudness and true-peak readings against a trusted reference signal.
