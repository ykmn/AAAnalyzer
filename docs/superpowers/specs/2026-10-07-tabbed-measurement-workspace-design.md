# Tabbed Measurement Workspace Design

## Goal

Replace the current four-pane dashboard with a compact source/True Peak rail
and a single tabbed instrument workspace. Add a common settings dialog whose
visualisation preferences persist across application launches.

## Layout

- The left rail is 130 px wide and contains the source selector, start/stop
  commands and two narrow true-peak meters.
- The workspace contains four tabs: `Waterfall`, `RTA`, `Loudness`, and
  `Phase scope`. The selected view takes all available workspace area.
- The header of every instrument has the same right-aligned actions: Reset,
  Screenshot and Settings.
- Reset clears every visual history and resets engine true-peak and loudness
  state. Screenshot saves only the active instrument pane.

## Instrument Rendering

- Waterfall uses equal-width L/R rectangles side by side, logarithmic 20 Hz to
  20 kHz mapping, a shared cursor and a bottom frequency scale. Its display
  floor, offset and palette come from settings.
- RTA renders selected mono/left/right spectrum as bars over a grid. The
  resolution selector remains in the tab header and is also exposed in
  settings.
- Loudness renders Momentary, Short-term and Integrated history over a
  sixty-second default window. It shows a large selected LUFS value, time
  labels, one-LUFS horizontal grid and a stable automatic vertical range.
- Phase scope stays square; the existing rotated transform preserves vertical
  mono and horizontal anti-phase display. Gain compensation is both a slider
  and a saved setting.
- The peak rail orders session maximum, current dBTP, overload latch, then the
  vertical bar. Maxima and overload latches have independent click resets.

## Settings Dialog

Each Settings action opens one modal dialog and selects the corresponding tab.
The dialog contains `Analyzer`, `Waterfall`, `Meters`, `Loudness`, `RTA` and
`Phase` pages:

| Page | Settings in this increment |
| --- | --- |
| Analyzer | FFT display floor and cursor/text colors; engine-changing FFT/window controls are visibly disabled until Core makes them configurable. |
| Waterfall | Display floor, display offset and palette. |
| Meters | Display range and meter colors. |
| Loudness | History duration, selected metric, fixed/automatic span and centre. |
| RTA | Source, resolution, scale top/range and target line. |
| Phase | Gain compensation. |

There is no preset system in this increment. Apply updates the live immutable
settings snapshot and saves it; OK applies and closes; Cancel restores the
snapshot captured when the dialog opened.

## Persistence and Failure Handling

`SettingsStore` loads and stores one JSON file at
`%LocalAppData%\AAAnalyzer\settings.json`. It writes a temporary file and
atomically replaces the target where supported. Missing, unreadable or invalid
settings fall back to defaults and are logged without preventing startup.

The settings object is immutable to rendering controls. A view-model owns the
currently applied snapshot, so UI updates occur on the dispatcher without
changing audio callbacks. Screenshot failures continue to log and never stop
analysis.

## Tests

- Settings defaults, JSON round trip and invalid-file fallback.
- Dialog Apply/Cancel behaviour through a settings edit session.
- Active tab selection and narrow peak rail geometry.
- Existing frequency, history, RTA and phase geometry tests remain in the
  suite.

## Scope Boundaries

This increment does not add user-defined presets or make Core FFT/window
parameters runtime-configurable. Controls for the latter are disabled and
labelled accordingly rather than silently ignored.
