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

    /// <summary>Raised when the plotted range changes, so the LU bar beside the plot can follow it.</summary>
    public event EventHandler<(double Minimum, double Maximum)>? RangeChanged;

    private (double Minimum, double Maximum)? _lastRange;

    public void Reset()
    {
        _history.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 11, 16)), null, new Rect(new Point(), RenderSize));
        var now = DateTimeOffset.Now;
        var points = _history.GetVisiblePoints(now);
        var visibleDuration = TimeSpan.FromSeconds(Math.Clamp(Settings.Loudness.HistorySeconds, 15, 43_200));
        points = points.Where(point => point.Timestamp >= now - visibleDuration).ToArray();
        Func<LoudnessHistoryPoint, float?> selected = point => LoudnessDisplayScale.SelectMetric(point, Settings.Loudness.Metric);
        var finite = points.Select(selected).Where(value => value.HasValue && float.IsFinite(value.Value)).Select(value => value!.Value).ToArray();
        var range = LoudnessDisplayScale.ResolveRange(Settings.Loudness, finite);
        if (range is null || ActualWidth <= 1 || ActualHeight <= WorkspaceLayout.PlotBottomReserve + TopGutter + 1) return;
        var (minimum, maximum) = range.Value;
        if (_lastRange != range) { _lastRange = range; RangeChanged?.Invoke(this, range.Value); }
        var plotBottom = PlotBottom;
        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)), 0.5);
        var timeStep = AxisTicks.LoudnessTimeStep(visibleDuration.TotalSeconds);
        foreach (var tick in AxisTicks.TimeTicks(now, visibleDuration, timeStep))
        {
            var x = ActualWidth * (1 - (tick.SecondsAgo / visibleDuration.TotalSeconds));
            context.DrawLine(gridPen, new Point(x, TopGutter), new Point(x, plotBottom));
            DrawLabel(context, tick.Label, x + 2, 2);
        }
        var valueStep = AxisTicks.LoudnessYStep(maximum - minimum);
        for (var lufs = Math.Ceiling(minimum / valueStep) * valueStep; lufs <= maximum; lufs += valueStep)
        {
            var y = Map(lufs, minimum, maximum);
            context.DrawLine(gridPen, new Point(0, y), new Point(ActualWidth, y));
            DrawLabel(context, $"{lufs:0} LUFS", 3, y + 1);
        }
        DrawSeries(context, points, selected, minimum, maximum, now, visibleDuration);
        DrawCaption(context);
    }

    private void DrawCaption(DrawingContext context)
    {
        var caption = Settings.Loudness.Metric switch
        {
            LoudnessMetric.Momentary => "Momentary Loudness",
            LoudnessMetric.ShortTerm => "Short-term Loudness",
            _ => "Integrated Loudness",
        };
        var text = new FormattedText(caption, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Bold, System.Windows.FontStretches.Normal),
            11, new SolidColorBrush(Color.FromRgb(110, 220, 120)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        context.DrawText(text, new Point(ActualWidth - text.Width - 6, ActualHeight - text.Height - 4));
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

    private const double TopGutter = WorkspaceLayout.LoudnessPlotTopGutter;

    private double PlotBottom => ActualHeight - WorkspaceLayout.PlotBottomReserve;

    private double Map(double value, double min, double max) => PlotBottom - ((value - min) / (max - min) * Math.Max(1, PlotBottom - TopGutter));

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
