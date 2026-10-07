using System.Windows.Media;
using System.Windows.Input;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Rendering;

public sealed class MeterRailView : FrameworkElement
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(MeasurementSettings), typeof(MeterRailView), new FrameworkPropertyMetadata(MeasurementSettings.Default, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(AnalysisFrame), typeof(MeterRailView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFrameChanged));
    private readonly MeteringHistory[] _meteringHistories = [new(), new()];
    private readonly MeterDisplayState[] _displayStates = [new(0, 0, 0), new(0, 0, 0)];

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
        var lufsValue = MeterRailLayout.SelectLoudness(Frame?.AdvancedMeasurements?.Loudness, Settings.Meters.LufsMetric);
        var fontSize = Settings.Meters.FontSize switch { MeterFontSize.Small => 8d, MeterFontSize.Medium => 10d, _ => 12d };
        for (var channel = 0; channel < 2; channel++)
        {
            var maximumBounds = channel == 0 ? layout.LeftMaximum : layout.RightMaximum;
            var overloadBounds = channel == 0 ? layout.LeftOverload : layout.RightOverload;
            var meter = channel == 0 ? layout.LeftMeter : layout.RightMeter;
            var x = maximumBounds.X;
            var overload = measurement?.Overload.ElementAtOrDefault(channel) == true;
            var current = measurement?.Current.ElementAtOrDefault(channel) ?? 0f;
            var maximum = measurement?.Maximum.ElementAtOrDefault(channel) ?? 0f;
            var display = _displayStates[channel];
            if (Settings.Meters.ShowPeakReadout)
            {
                DrawText(context, $"MAX {ToDb(maximum):0.0}", x, 4, fontSize, Brushes.LightGray);
                DrawText(context, $"{ToDb(current):0.0} dBTP", x, 18, fontSize, Brushes.White);
            }
            if (Settings.Meters.ShowClipIndicator)
            {
                context.DrawRectangle(overload ? ColorBrush(Settings.Meters.ClipColor) : Brushes.DarkRed, null, overloadBounds);
            }
            DrawText(context, channel == 0 ? "L" : "R", x, 43, fontSize, Brushes.LightGray);
            if (Settings.Meters.ShowLkfsReadout)
            {
                DrawText(context, lufsValue.HasValue ? $"{lufsValue.Value:0.0} LKFS" : "— LKFS", x + 18, 43, fontSize, ColorBrush(Settings.Meters.LufsColor));
            }
            context.DrawRectangle(Brushes.Black, new Pen(Brushes.DimGray, 1), meter);
            var peakRatio = MeterRailLayout.CalculateFillRatio(display.Peak, Settings.Meters.DisplayRangeDb);
            var peakFill = peakRatio * meter.Height;
            context.DrawRectangle(ColorBrush(Settings.Meters.PeakColor), null, new Rect(meter.Left + 2, meter.Bottom - peakFill, meter.Width - 4, peakFill));
            if (Settings.Meters.ShowRmsBars)
            {
                var rmsRatio = MeterRailLayout.CalculateFillRatio(display.Rms, Settings.Meters.DisplayRangeDb);
                var rmsFill = rmsRatio * meter.Height;
                var rmsWidth = Math.Max(2, (meter.Width - 4) / 3);
                context.DrawRectangle(ColorBrush(Settings.Meters.RmsColor), null, new Rect(meter.Left + (meter.Width - rmsWidth) / 2, meter.Bottom - rmsFill, rmsWidth, rmsFill));
            }
            var holdRatio = MeterRailLayout.CalculateFillRatio(display.PeakHold, Settings.Meters.DisplayRangeDb);
            var holdY = meter.Bottom - (holdRatio * meter.Height);
            context.DrawLine(new Pen(Brushes.White, 1), new Point(meter.Left + 1, holdY), new Point(meter.Right - 1, holdY));
            var lufsScale = MeterRailLayout.ResolveLufsScale(Settings.Meters);
            if (Settings.Meters.ShowLufsIndicator && lufsValue.HasValue)
            {
                var lufsRatio = MeterRailLayout.CalculateLufsRatio(lufsValue.Value, lufsScale);
                var lufsY = meter.Bottom - (lufsRatio * meter.Height);
                context.DrawLine(new Pen(ColorBrush(Settings.Meters.LufsColor), 2), new Point(meter.Left, lufsY), new Point(meter.Right, lufsY));
            }
            if (Settings.Meters.ShowLufsIndicator)
            {
                foreach (var tick in MeterRailLayout.CreateLufsScaleTicks(Settings.Meters))
                {
                    var tickY = meter.Bottom - (MeterRailLayout.CalculateLufsRatio(tick, lufsScale) * meter.Height);
                    context.DrawLine(new Pen(Brushes.LightGray, 0.75), new Point(meter.Left, tickY), new Point(meter.Left + 4, tickY));
                }
            }
            if (Settings.Meters.ShowDbScale)
            {
                var rangeDb = Math.Max(1d, Math.Abs(Settings.Meters.DisplayRangeDb));
                for (var db = 0d; db <= rangeDb; db += 10d)
                {
                    var y = meter.Bottom - ((db / rangeDb) * meter.Height);
                    context.DrawLine(new Pen(Brushes.Gray, 0.5), new Point(meter.Left, y), new Point(meter.Right, y));
                }
            }
        }
    }

    private static void OnFrameChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var view = (MeterRailView)target;
        var frame = args.NewValue as AnalysisFrame;
        var truePeak = frame?.AdvancedMeasurements?.TruePeak;
        for (var channel = 0; channel < view._meteringHistories.Length; channel++)
        {
            var level = frame?.Levels.ElementAtOrDefault(channel) ?? new SystemAudioAnalyzer.Core.ChannelLevel(0, 0);
            var peak = truePeak?.Current.ElementAtOrDefault(channel) ?? level.Peak;
            view._displayStates[channel] = view._meteringHistories[channel].Update(new SystemAudioAnalyzer.Core.ChannelLevel(peak, level.Rms), frame?.Timestamp ?? DateTimeOffset.UtcNow, view.Settings.Meters);
        }
    }

    private void DrawText(DrawingContext context, string text, double x, double y, double size, Brush brush) =>
        context.DrawText(new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(x, y));

    private static double ToDb(float value) => !float.IsFinite(value) || value <= 0 ? -120d : 20 * Math.Log10(value);

    private static Brush ColorBrush(string color) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}

public enum MeterRailResetTarget { Maximum, Overload }

public sealed class MeterRailResetEventArgs(int channel, MeterRailResetTarget target) : EventArgs
{
    public int Channel { get; } = channel;
    public MeterRailResetTarget Target { get; } = target;
}
