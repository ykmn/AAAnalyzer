# WPF GUI Design

## Goal

Create the first desktop interface for AAAnalyzer: select a local output device
or Icecast/HLS URL, start and stop analysis, and show real-time RTA, stereo
meters, and a two-channel Waterfall.

## Scope

Included:

- WPF application using MVVM;
- source selector with `Device` and `URL stream` modes;
- start/stop lifecycle and visible connection/buffering/fault status;
- RTA, peak/RMS meters, and two-channel Waterfall;
- Icecast/HLS decoding through LibVLCSharp and LibVLC;
- adaptive layout that preserves both Waterfall channels at equal width.

Excluded:

- recording, per-process capture, loudness history, equalization, playlists,
  or stream playback controls;
- replacing Core analysis algorithms;
- automatic retry after a source fault.

## Architecture

`SystemAudioAnalyzer.App` is a WPF executable that owns views, ViewModels and
presentation-only rendering code. `SystemAudioAnalyzer.Core` remains independent
from WPF and receives normalised PCM frames.

The App uses an `IAudioSource` abstraction. A local-output implementation wraps
the current WASAPI capture path. A network implementation uses LibVLCSharp audio
callbacks configured to provide PCM. Both implementations publish the same
sample format and lifecycle events to an adapter that feeds `AudioAnalysisEngine`.

```text
Device source / LibVLC network source
  -> normalised PCM source adapter
  -> AudioAnalysisEngine
  -> AnalysisFrame stream
  -> ViewModel (Dispatcher)
  -> WPF meters / RTA / Waterfall
```

The network source configures a short bounded cache: 100–250 ms for Icecast and
the smallest stable value for HLS. HLS cannot be fresher than the playlist and
segment cadence offered by its server; LL-HLS is required for genuinely low
end-to-end latency.

## Source lifecycle

The user-visible source state is one of `Idle`, `Connecting`, `Buffering`,
`Running`, `Stopping`, or `Faulted`.

- `Start` validates the chosen source and begins acquisition.
- `Stop`, changing mode, changing device, closing the main window, and a fault
  all cancel the current operation and dispose its native resources.
- LibVLC buffering and error events update the state through the ViewModel.
- Faults include an actionable status message and leave Start available for a
  manual retry.
- The PCM and analysis queues are bounded. When overloaded, the oldest queued
  data is discarded to preserve live behaviour rather than accumulate latency.

## Main window

The upper toolbar contains:

- source-mode toggle: `Device` or `URL stream`;
- either an output-device ComboBox or an HTTP/HTTPS URL TextBox;
- Start/Stop button;
- state indicator and short status text.

The workspace contains a thin L/R meter rail plus tabs for `RTA` and
`Waterfall`.

`RTA` renders the latest spectrum against a logarithmic frequency axis.
`Waterfall` always has two equal-width panels: L on the left and R on the
right. Each renderer works in normalised coordinates and redraws for the actual
available size; no fixed pixel bitmap is stretched or cropped. Therefore both
channels retain equal area and proportionally scaled content throughout a
resize.

The mockup approved in the design discussion is the visual reference; the
application uses its dark instrument-panel style without reproducing the
reference application itself.

## Testing and acceptance

Automated tests cover:

- ViewModel source mode, URL validation, command enablement and lifecycle
  transitions;
- source adapter forwarding of PCM and state/error events;
- safe cancellation and disposal during a source change;
- Waterfall viewport calculations for equal L/R dimensions and resize.

Manual checks cover:

- local output with silence and playback;
- a reachable Icecast URL and HLS URL;
- status while connecting, buffering, running and after a bad URL;
- repeated Start/Stop and source switching;
- narrow and wide window resize while Waterfall remains equally split.

The GUI is accepted when each source produces continuously updating meter, RTA
and Waterfall data without blocking the window, and when analysis visibly stays
near live playback rather than falling behind under normal load.
