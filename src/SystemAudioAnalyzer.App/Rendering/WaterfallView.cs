using System.Windows.Input;
using System.Windows.Media;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class WaterfallView : FrameworkElement
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(MeasurementSettings), typeof(WaterfallView), new FrameworkPropertyMetadata(MeasurementSettings.Default, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(AnalysisFrame), typeof(WaterfallView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFrameChanged));

    private readonly WaterfallHistory _history = new(TimeSpan.FromSeconds(10));
    private double _cursor = 0.5;

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
        context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(layout.RightBounds.Left, layout.LeftBounds.Top), new Point(layout.RightBounds.Left, layout.LeftBounds.Bottom));
        DrawCursor(context, layout.LeftBounds);
        DrawCursor(context, layout.RightBounds);
        DrawText(context, FrequencyScale.Format(FrequencyScale.ToHertz(_cursor)), 6, 4, 12, Brushes.White);
        foreach (var hertz in new[] { 20d, 100d, 1_000d, 10_000d, 20_000d })
        {
            var x = FrequencyScale.ToNormalized(hertz) * layout.LeftBounds.Width;
            context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(x, layout.LeftBounds.Bottom), new Point(x, layout.LeftBounds.Bottom + 4));
            var label = FormatTick(hertz);
            var labelWidth = label.Length * 5.4;
            var labelX = hertz >= 10_000 ? x - labelWidth - 2 : x + 2;
            DrawText(context, label, labelX, layout.LeftBounds.Bottom + 5, 9, Brushes.LightGray);
        }
    }

    private static void OnFrameChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is AnalysisFrame frame) ((WaterfallView)target)._history.Append(frame);
    }

    private void OnMouseMove(object sender, MouseEventArgs args)
    {
        var paneWidth = Math.Max(1, ActualWidth / 2d);
        _cursor = Math.Clamp((args.GetPosition(this).X % paneWidth) / paneWidth, 0, 1);
        InvalidateVisual();
    }

    private void DrawRows(DrawingContext context, IReadOnlyList<WaterfallRow> rows, Rect bounds, bool left, DateTimeOffset now)
    {
        if (rows.Count == 0) return;
        for (var row = 0; row < rows.Count; row++)
        {
            var ageSeconds = Math.Clamp((now - rows[row].Timestamp).TotalSeconds, 0, 10);
            var y = bounds.Bottom - (ageSeconds / 10d * bounds.Height);
            var rowHeight = Math.Max(1, bounds.Height / 300d);
            var values = left ? rows[row].Left : rows[row].Right;
            for (var x = 0; x < 96; x++)
            {
                var lowerHertz = FrequencyScale.ToHertz((double)x / 96);
                var upperHertz = FrequencyScale.ToHertz((double)(x + 1) / 96);
                var firstBin = Math.Max(0, (int)Math.Floor(lowerHertz * rows[row].FftSize / rows[row].SampleRate));
                var lastBin = Math.Min(values.Count - 1, (int)Math.Ceiling(upperHertz * rows[row].FftSize / rows[row].SampleRate));
                var magnitude = 0f;
                for (var bin = firstBin; bin <= lastBin; bin++) magnitude = Math.Max(magnitude, values[bin]);
                var db = values.Count == 0 ? Settings.Waterfall.DisplayFloorDb : 20 * Math.Log10(Math.Max(magnitude, 0.000001f)) + Settings.Waterfall.DisplayOffsetDb;
                var intensity = Math.Clamp((db - Settings.Waterfall.DisplayFloorDb) / Math.Max(1, 0 - Settings.Waterfall.DisplayFloorDb), 0, 1);
                var baseColor = (Color)ColorConverter.ConvertFromString(Settings.Waterfall.PaletteColor);
                var brush = new SolidColorBrush(Color.FromRgb((byte)(baseColor.R * intensity), (byte)(baseColor.G * intensity), (byte)(baseColor.B * intensity)));
                brush.Freeze();
                context.DrawRectangle(brush, null, new Rect(bounds.Left + x * bounds.Width / 96, y - rowHeight, bounds.Width / 96 + 1, rowHeight + 0.2));
            }
        }
    }

    private void DrawCursor(DrawingContext context, Rect bounds)
    {
        var x = bounds.Left + bounds.Width * _cursor;
        context.DrawLine(new Pen(Brushes.White, 1), new Point(x, bounds.Top), new Point(x, bounds.Bottom));
    }

    private void DrawText(DrawingContext context, string text, double x, double y, double size, Brush brush) =>
        context.DrawText(new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));

    private static string FormatTick(double hertz) => hertz switch
    {
        < 1_000 => hertz.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " Hz",
        _ => (hertz / 1_000).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "k",
    };
}
