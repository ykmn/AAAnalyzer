using System.Windows.Input;
using System.Windows.Media;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class WaterfallView : FrameworkElement
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(MeasurementSettings), typeof(WaterfallView), new FrameworkPropertyMetadata(MeasurementSettings.Default, FrameworkPropertyMetadataOptions.AffectsRender, OnSettingsChanged));
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(AnalysisFrame), typeof(WaterfallView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFrameChanged));

    private readonly WaterfallHistory _history = new(TimeSpan.FromSeconds(10));
    private double _cursor = 0.5;
    private SolidColorBrush[] _palette = WaterfallRenderer.CreatePalette(
        MeasurementSettings.Default.Waterfall.DisplayFloorDb,
        MeasurementSettings.Default.Waterfall.DisplayOffsetDb,
        MeasurementSettings.Default.Waterfall.GradientStops);

    public WaterfallView()
    {
        MouseMove += OnMouseMove;
    }

    public AnalysisFrame? Frame
    {
        get => (AnalysisFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public MeasurementSettings Settings { get => (MeasurementSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }

    public void Reset()
    {
        _history.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(11, 18, 32)), null, new Rect(new Point(), RenderSize));
        if (ActualWidth <= 1 || ActualHeight <= 1) return;
        var layout = WaterfallLayout.Calculate(ActualWidth, Math.Max(1, ActualHeight - 30));
        var rows = _history.GetVisibleRows(DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow;
        DrawRows(context, rows, layout.LeftBounds, true, now);
        DrawRows(context, rows, layout.RightBounds, false, now);
        var stereo = Frame?.AdvancedMeasurements?.StereoSpectrum;
        DrawSpectrumOverlay(context, stereo?.Left ?? Frame?.Spectrum, layout.LeftBounds);
        DrawSpectrumOverlay(context, stereo?.Right ?? Frame?.Spectrum, layout.RightBounds);
        context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(layout.RightBounds.Left, layout.LeftBounds.Top), new Point(layout.RightBounds.Left, layout.LeftBounds.Bottom));
        DrawCursor(context, layout.LeftBounds);
        DrawCursor(context, layout.RightBounds);
        DrawText(context, FrequencyScale.Format(FrequencyScale.ToHertz(_cursor, Settings.Analyzer.FrequencyScale)), 6, 4, 12, Brushes.White);
        foreach (var hertz in new[] { 20d, 100d, 1_000d, 10_000d, 20_000d })
        {
            var x = FrequencyScale.ToNormalized(hertz, Settings.Analyzer.FrequencyScale) * layout.LeftBounds.Width;
            context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(x, layout.LeftBounds.Bottom), new Point(x, layout.LeftBounds.Bottom + 4));
            var label = FrequencyScale.Format(hertz);
            var labelWidth = label.Length * 5.4;
            var labelX = hertz >= 10_000 ? x - labelWidth - 2 : x + 2;
            DrawText(context, label, labelX, layout.LeftBounds.Bottom + 5, 9, Brushes.LightGray);
        }
    }

    private static void OnFrameChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is AnalysisFrame frame) ((WaterfallView)target)._history.Append(frame);
    }

    private static void OnSettingsChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var view = (WaterfallView)target;
        var waterfall = view.Settings.Waterfall;
        var floor = SpectrumDisplayScale.EffectiveFloor(view.Settings.Analyzer.DisplayFloorDb, waterfall.DisplayFloorDb);
        view._palette = WaterfallRenderer.CreatePalette(floor, waterfall.DisplayOffsetDb, waterfall.GradientStops);
    }

    private void OnMouseMove(object sender, MouseEventArgs args)
    {
        if (ActualWidth <= 1) return;
        var layout = WaterfallLayout.Calculate(ActualWidth, Math.Max(1, ActualHeight));
        _cursor = layout.GetNormalizedX(args.GetPosition(this).X);
        InvalidateVisual();
    }

    private void DrawRows(DrawingContext context, IReadOnlyList<WaterfallRow> rows, Rect bounds, bool left, DateTimeOffset now)
    {
        if (rows.Count == 0) return;
        var displayFloor = SpectrumDisplayScale.EffectiveFloor(Settings.Analyzer.DisplayFloorDb, Settings.Waterfall.DisplayFloorDb);
        for (var row = 0; row < rows.Count; row++)
        {
            var ageSeconds = Math.Clamp((now - rows[row].Timestamp).TotalSeconds, 0, 10);
            var y = bounds.Bottom - (ageSeconds / 10d * bounds.Height);
            var rowHeight = Math.Max(1, bounds.Height / 300d);
            var values = left ? rows[row].Left : rows[row].Right;
            for (var x = 0; x < 96; x++)
            {
                var lowerHertz = FrequencyScale.ToHertz((double)x / 96, Settings.Analyzer.FrequencyScale);
                var upperHertz = FrequencyScale.ToHertz((double)(x + 1) / 96, Settings.Analyzer.FrequencyScale);
                var firstBin = Math.Max(0, (int)Math.Floor(lowerHertz * rows[row].FftSize / rows[row].SampleRate));
                var lastBin = Math.Min(values.Count - 1, (int)Math.Ceiling(upperHertz * rows[row].FftSize / rows[row].SampleRate));
                var magnitude = 0f;
                for (var bin = firstBin; bin <= lastBin; bin++) magnitude = Math.Max(magnitude, values[bin]);
                var db = values.Count == 0 ? displayFloor : 20 * Math.Log10(Math.Max(magnitude * Settings.Analyzer.Gain, 0.000001f));
                var paletteIndex = WaterfallRenderer.GetPaletteIndex(db, displayFloor, Settings.Waterfall.DisplayOffsetDb, Settings.Waterfall.GradientStops, _palette.Length);
                context.DrawRectangle(_palette[paletteIndex], null, new Rect(bounds.Left + x * bounds.Width / 96, y - rowHeight, bounds.Width / 96 + 1, rowHeight + 0.2));
            }
        }
    }

    private void DrawCursor(DrawingContext context, Rect bounds)
    {
        var x = bounds.Left + bounds.Width * _cursor;
        context.DrawLine(new Pen(ColorBrush(Settings.Analyzer.CursorColor), 1), new Point(x, bounds.Top), new Point(x, bounds.Bottom));
    }

    private void DrawSpectrumOverlay(DrawingContext context, Spectrum? spectrum, Rect bounds)
    {
        if (spectrum is null || spectrum.Magnitudes.Count < 2) return;
        var geometry = new StreamGeometry();
        using (var drawing = geometry.Open())
        {
            var started = false;
            for (var bin = 1; bin < spectrum.Magnitudes.Count; bin++)
            {
                var hertz = spectrum.GetFrequencyHz(bin);
                if (hertz < FrequencyScale.MinimumHertz) continue;
                if (hertz > FrequencyScale.MaximumHertz) break;
                var normalizedX = FrequencyScale.ToNormalized(hertz, Settings.Analyzer.FrequencyScale);
                var normalizedY = SpectrumDisplayScale.ToNormalizedAmplitude(spectrum.Magnitudes[bin], Settings.Analyzer.Gain,
                    Settings.Analyzer.AmplitudeScale, Settings.Analyzer.DisplayFloorDb);
                var point = new Point(bounds.Left + normalizedX * bounds.Width, bounds.Bottom - normalizedY * bounds.Height);
                if (!started)
                {
                    drawing.BeginFigure(point, false, false);
                    started = true;
                }
                else
                {
                    drawing.LineTo(point, true, false);
                }
            }
        }

        geometry.Freeze();
        context.DrawGeometry(null, new Pen(ColorBrush(Settings.Analyzer.CursorColor), 1), geometry);
    }

    private void DrawText(DrawingContext context, string text, double x, double y, double size, Brush brush) =>
        context.DrawText(new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, ColorBrush(Settings.Analyzer.TextColor), VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));

    private static Brush ColorBrush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}
