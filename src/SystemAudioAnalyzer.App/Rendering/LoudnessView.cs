using System.Windows.Media;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class LoudnessView : FrameworkElement
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(MeasurementSettings), typeof(LoudnessView), new FrameworkPropertyMetadata(MeasurementSettings.Default, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(nameof(Frame), typeof(AnalysisFrame), typeof(LoudnessView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFrameChanged));
    private readonly LoudnessHistory _history = new(TimeSpan.FromSeconds(43_200));

    public AnalysisFrame? Frame { get => (AnalysisFrame?)GetValue(FrameProperty); set => SetValue(FrameProperty, value); }
    public MeasurementSettings Settings { get => (MeasurementSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }

    public void Reset()
    {
        _history.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 24, 39)), null, new Rect(new Point(), RenderSize));
        var now = DateTimeOffset.UtcNow;
        var points = _history.GetVisiblePoints(now);
        var visibleDuration = TimeSpan.FromSeconds(Math.Clamp(Settings.Loudness.HistorySeconds, 15, 43_200));
        points = points.Where(point => point.Timestamp >= now - visibleDuration).ToArray();
        Func<LoudnessHistoryPoint, float?> selected = point => LoudnessDisplayScale.SelectMetric(point, Settings.Loudness.Metric);
        var finite = points.Select(selected).Where(value => value.HasValue && float.IsFinite(value.Value)).Select(value => value!.Value).ToArray();
        var range = LoudnessDisplayScale.ResolveRange(Settings.Loudness, finite);
        if (range is null || ActualWidth <= 1 || ActualHeight <= 1) return;
        var (minimum, maximum) = range.Value;
        var timeStep = Math.Max(10d, Math.Ceiling(visibleDuration.TotalSeconds / 120d) * 10d);
        for (var second = 0d; second <= visibleDuration.TotalSeconds; second += timeStep)
        {
            var x = ActualWidth * (visibleDuration.TotalSeconds - second) / visibleDuration.TotalSeconds;
            context.DrawLine(new Pen(Brushes.DimGray, 0.5), new Point(x, 0), new Point(x, ActualHeight));
            var labelTime = now.AddSeconds(-second).ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            DrawLabel(context, labelTime, x + 2, ActualHeight - 14);
        }
        for (var lufs = minimum; lufs <= maximum; lufs++)
        {
            var y = Map(lufs, minimum, maximum);
            context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(0, y), new Point(ActualWidth, y));
            DrawLabel(context, $"{lufs:0} LUFS", 3, y - 12);
        }
        DrawSeries(context, points, selected, minimum, maximum, now, visibleDuration);
    }

    private static void OnFrameChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is AnalysisFrame frame && frame.AdvancedMeasurements is { } measurements)
            ((LoudnessView)target)._history.Append(frame.Timestamp, measurements.Loudness);
    }

    private void DrawSeries(DrawingContext context, IReadOnlyList<LoudnessHistoryPoint> points, Func<LoudnessHistoryPoint, float?> select, double min, double max, DateTimeOffset now, TimeSpan visibleDuration)
    {
        Point? previous = null;
        for (var index = 0; index < points.Count; index++)
        {
            var value = select(points[index]);
            if (!value.HasValue) { previous = null; continue; }
            var secondsAgo = (now - points[index].Timestamp).TotalSeconds;
            if (!float.IsFinite(value.Value)) { previous = null; continue; }
            var current = new Point(ActualWidth * (1 - secondsAgo / visibleDuration.TotalSeconds), Map(value.Value, min, max));
            if (previous is not null) context.DrawLine(new Pen(new SolidColorBrush(ColorGradient.Sample(Settings.Loudness.GradientStops, value.Value)), 1.5), previous.Value, current);
            previous = current;
        }
    }

    private double Map(double value, double min, double max) => ActualHeight - ((value - min) / (max - min) * ActualHeight);

    private void DrawLabel(DrawingContext context, string text, double x, double y) =>
        context.DrawText(new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 9, Brushes.LightGray, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));
}

public static class LoudnessDisplayScale
{
    public static (double Minimum, double Maximum)? ResolveRange(LoudnessDisplaySettings settings, IReadOnlyList<float> selectedValues)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(selectedValues);
        if (!settings.AutoScale)
        {
            var minimum = settings.CentreLufs - (settings.SpanLufs / 2d);
            var maximum = settings.CentreLufs + (settings.SpanLufs / 2d);
            return double.IsFinite(minimum) && double.IsFinite(maximum) && maximum > minimum ? (minimum, maximum) : null;
        }

        var finite = selectedValues.Where(float.IsFinite).ToArray();
        if (finite.Length == 0) return null;
        var automaticMinimum = Math.Floor(finite.Min()) - 1d;
        var automaticMaximum = Math.Ceiling(finite.Max()) + 1d;
        return double.IsFinite(automaticMinimum) && double.IsFinite(automaticMaximum) && automaticMaximum > automaticMinimum
            ? (automaticMinimum, automaticMaximum)
            : null;
    }

    public static float? SelectMetric(LoudnessHistoryPoint point, LoudnessMetric metric) => metric switch
    {
        LoudnessMetric.Momentary => point.MomentaryLufs,
        LoudnessMetric.ShortTerm => point.ShortTermLufs,
        _ => point.IntegratedLufs,
    };
}
