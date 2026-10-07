using System.Windows.Media;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class MeterRailView : FrameworkElement
{
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(AnalysisFrame), typeof(MeterRailView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public AnalysisFrame? Frame
    {
        get => (AnalysisFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 24, 39)), null, new Rect(new Point(), RenderSize));
        var measurement = Frame?.AdvancedMeasurements?.TruePeak;
        for (var channel = 0; channel < 2; channel++)
        {
            var x = channel * ActualWidth / 2d + 8;
            var width = Math.Max(1, ActualWidth / 2d - 16);
            var overload = measurement?.Overload.ElementAtOrDefault(channel) == true;
            var current = measurement?.Current.ElementAtOrDefault(channel) ?? 0f;
            var maximum = measurement?.Maximum.ElementAtOrDefault(channel) ?? 0f;
            DrawText(context, $"MAX {ToDb(maximum):0.0}", x, 4, 9, Brushes.LightGray);
            DrawText(context, $"{ToDb(current):0.0} dBTP", x, 18, 9, Brushes.White);
            context.DrawRectangle(overload ? Brushes.Red : Brushes.DarkRed, null, new Rect(x, 32, width, 9));
            DrawText(context, channel == 0 ? "L" : "R", x, 43, 9, Brushes.LightGray);
            var meter = new Rect(x, 56, width, Math.Max(1, ActualHeight - 62));
            context.DrawRectangle(Brushes.Black, new Pen(Brushes.DimGray, 1), meter);
            var fill = Math.Clamp((ToDb(current) + 60) / 60, 0, 1) * meter.Height;
            context.DrawRectangle(Brushes.LimeGreen, null, new Rect(meter.Left + 2, meter.Bottom - fill, meter.Width - 4, fill));
        }
    }

    private void DrawText(DrawingContext context, string text, double x, double y, double size, Brush brush) =>
        context.DrawText(new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));

    private static double ToDb(float value) => 20 * Math.Log10(Math.Max(value, 0.000001f));
}
