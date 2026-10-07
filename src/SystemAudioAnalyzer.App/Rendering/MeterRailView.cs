using System.Windows.Media;
using System.Windows.Input;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class MeterRailView : FrameworkElement
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(MeasurementSettings), typeof(MeterRailView), new FrameworkPropertyMetadata(MeasurementSettings.Default, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(AnalysisFrame), typeof(MeterRailView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public AnalysisFrame? Frame
    {
        get => (AnalysisFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public MeasurementSettings Settings { get => (MeasurementSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }

    public event EventHandler<MeterRailResetEventArgs>? ResetRequested;

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs eventArgs)
    {
        var point = eventArgs.GetPosition(this);
        var layout = MeterRailLayout.Calculate(ActualWidth, ActualHeight);
        if (layout.LeftMaximum.Contains(point)) ResetRequested?.Invoke(this, new MeterRailResetEventArgs(0, MeterRailResetTarget.Maximum));
        else if (layout.RightMaximum.Contains(point)) ResetRequested?.Invoke(this, new MeterRailResetEventArgs(1, MeterRailResetTarget.Maximum));
        else if (layout.LeftOverload.Contains(point)) ResetRequested?.Invoke(this, new MeterRailResetEventArgs(0, MeterRailResetTarget.Overload));
        else if (layout.RightOverload.Contains(point)) ResetRequested?.Invoke(this, new MeterRailResetEventArgs(1, MeterRailResetTarget.Overload));
        else { base.OnMouseLeftButtonDown(eventArgs); return; }
        eventArgs.Handled = true;
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 24, 39)), null, new Rect(new Point(), RenderSize));
        var measurement = Frame?.AdvancedMeasurements?.TruePeak;
        var layout = MeterRailLayout.Calculate(ActualWidth, ActualHeight);
        for (var channel = 0; channel < 2; channel++)
        {
            var maximumBounds = channel == 0 ? layout.LeftMaximum : layout.RightMaximum;
            var overloadBounds = channel == 0 ? layout.LeftOverload : layout.RightOverload;
            var meter = channel == 0 ? layout.LeftMeter : layout.RightMeter;
            var x = maximumBounds.X;
            var width = maximumBounds.Width;
            var overload = measurement?.Overload.ElementAtOrDefault(channel) == true;
            var current = measurement?.Current.ElementAtOrDefault(channel) ?? 0f;
            var maximum = measurement?.Maximum.ElementAtOrDefault(channel) ?? 0f;
            DrawText(context, $"MAX {ToDb(maximum):0.0}", x, 4, 9, Brushes.LightGray);
            DrawText(context, $"{ToDb(current):0.0} dBTP", x, 18, 9, Brushes.White);
            context.DrawRectangle(overload ? ColorBrush(Settings.Meters.OverloadColor) : Brushes.DarkRed, null, overloadBounds);
            DrawText(context, channel == 0 ? "L" : "R", x, 43, 9, Brushes.LightGray);
            context.DrawRectangle(Brushes.Black, new Pen(Brushes.DimGray, 1), meter);
            var range = Math.Max(1, Settings.Meters.DisplayRangeDb);
            var fill = Math.Clamp((ToDb(current) + range) / range, 0, 1) * meter.Height;
            context.DrawRectangle(ColorBrush(Settings.Meters.MeterColor), null, new Rect(meter.Left + 2, meter.Bottom - fill, meter.Width - 4, fill));
        }
    }

    private void DrawText(DrawingContext context, string text, double x, double y, double size, Brush brush) =>
        context.DrawText(new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));

    private static double ToDb(float value) => 20 * Math.Log10(Math.Max(value, 0.000001f));

    private static Brush ColorBrush(string color) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}

public enum MeterRailResetTarget { Maximum, Overload }

public sealed class MeterRailResetEventArgs(int channel, MeterRailResetTarget target) : EventArgs
{
    public int Channel { get; } = channel;
    public MeterRailResetTarget Target { get; } = target;
}
