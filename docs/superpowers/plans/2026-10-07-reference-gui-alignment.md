# Reference GUI Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox syntax.

**Goal:** Привести Waterfall, Loudness, RTA и Peak rail к референс-скриншотам (spec: `docs/superpowers/specs/2026-10-07-reference-gui-alignment-design.md`).

**Architecture:** Чистая логика (шкалы, подписи, раскладка, буфер Waterfall, действия тулбаров) — в тестируемых классах без WPF-отрисовки; View-классы только рисуют. Waterfall рендерится в `WriteableBitmap`.

**Tech Stack:** .NET 8 WPF, xUnit. Тесты: `dotnet test tests/SystemAudioAnalyzer.App.Tests`.

## Global Constraints

- Ширина Peak rail/левой колонки 130 px (`WorkspaceLayout.PeakRailWidth`).
- Waterfall: L сверху, R снизу; новая строка появляется сверху и сдвигается вниз.
- Окно Waterfall 10 с; диалоги настроек и Core не меняются.
- `TreatWarningsAsErrors` включён в тестах.

## Отступления от spec (осознанные)

- Строка вкладок — компактная плоская строка 26 px, а не прозрачный оверлей на графике.
- Кнопка «Rolling» циклически переключает окно Integrated (60/300/600/1800/3600 с) и выбирает метрику Integrated.
- Изменения из тулбаров живут в `MainViewModel.MeasurementSettings` (runtime); в профиль попадают через диалог настроек.
- Overlay-кривая спектра на Waterfall убрана (в референсе её нет).

## Tasks

1. **AxisTicks + RtaSegments** (`Rendering/AxisTicks.cs`, `Rendering/RtaSegments.cs`): тики dB рейла, LU-метки, подписи частот RTA/Waterfall, dB-оси RTA, шаги сетки Loudness, временные тики в локальном времени. Тесты `AxisTicksTests`.
2. **ToolbarSettingsActions** (`Settings/ToolbarSettingsActions.cs`) + свойства `MainViewModel` (`LoudnessMetric`, `LoudnessWindowSeconds`, zoom/shift/rolling, `AdjustRtaAveraging`, `AdjustRtaTarget`, public `ActiveTab`). Тесты.
3. **MeterRailLayout**: новая геометрия (шкала dB, 2 бара, LUFS-колонка, readout). Тесты.
4. **MeterRailView**: отрисовка по новой раскладке.
5. **WaterfallBitmapBuffer** (скролл сверху вниз). Тесты.
6. **WaterfallRowPixelizer** + `WaterfallRenderer.CreateArgbPalette`. Тесты.
7. **WaterfallLayout** → вертикальное разделение L/R + ось. Обновить тесты.
8. **WaterfallView** на `WriteableBitmap`, курсор-плашка.
9. **RtaView**: оси, сегментированные столбики.
10. **LoudnessView**: временные метки сверху (локальные), шаг сетки, подпись кривой.
11. **MainWindow/App.xaml**: стили, строка вкладок, тулбары RTA/Loudness, `ValueMatchConverter`.
12. Документация (TODO/DONE/CHANGELOG/VERSION), полный прогон тестов, коммит.
