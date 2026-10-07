using System.Windows;
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
        var target = MeterRailLayout.Calculate(ActualWidth, ActualHeight).HitTest(eventArgs.GetPosition(this));
        if (target is null)
        {
            base.OnMouseLeftButtonDown(eventArgs);
            return;
        }

        // The reset applies to both channels, whichever readout or lamp was clicked.
        for (var channel = 0; channel < 2; channel++) ResetRequested?.Invoke(this, new MeterRailResetEventArgs(channel, target.Value));
        eventArgs.Handled = true;
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(new SolidColorBrush(Color.FromRgb(8, 11, 16)), null, new Rect(new Point(), RenderSize));
        var measurement = Frame?.AdvancedMeasurements?.TruePeak;
        var layout = MeterRailLayout.Calculate(ActualWidth, ActualHeight);
        var meters = Settings.Meters;
        var lufsValue = MeterRailLayout.SelectLoudness(Frame?.AdvancedMeasurements?.Loudness, meters.LufsMetric);
        var fontSize = meters.FontSize switch { MeterFontSize.Small => 8d, MeterFontSize.Medium => 9d, _ => 10d };
        var lufsScale = MeterRailLayout.ResolveLufsScale(meters);
        var lufsBrush = ColorBrush(meters.LufsColor);
        var dimText = new SolidColorBrush(Color.FromRgb(120, 135, 150));
        if (meters.ShowPeakReadout)
        {
            DrawText(context, "MAX", new Rect(layout.DbScale.Left, 2, layout.DbScale.Width, 12), fontSize - 1, new SolidColorBrush(Color.FromRgb(255, 190, 90)), TextAlignment.Right);
            DrawText(context, "NOW", new Rect(layout.DbScale.Left, 15, layout.DbScale.Width, 12), fontSize - 1, dimText, TextAlignment.Right);
        }
        if (meters.ShowDbScale)
        {
            foreach (var tick in AxisTicks.PeakRailDb(meters.DisplayRangeDb))
            {
                var y = layout.LeftMeter.Bottom - (tick.Ratio * layout.LeftMeter.Height);
                DrawText(context, tick.Label, new Rect(layout.DbScale.Left, y - 6, layout.DbScale.Width, 12), fontSize - 1, dimText, TextAlignment.Right);
            }
        }
        for (var channel = 0; channel < 2; channel++)
        {
            var maximumBounds = channel == 0 ? layout.LeftMaximum : layout.RightMaximum;
            var overloadBounds = channel == 0 ? layout.LeftOverload : layout.RightOverload;
            var meter = channel == 0 ? layout.LeftMeter : layout.RightMeter;
            var overload = measurement?.Overload.ElementAtOrDefault(channel) == true;
            var maximum = measurement?.Maximum.ElementAtOrDefault(channel) ?? 0f;
            var currentPeak = measurement?.Current.ElementAtOrDefault(channel) ?? 0f;
            var currentBounds = channel == 0 ? layout.LeftCurrent : layout.RightCurrent;
            var display = _displayStates[channel];
            if (meters.ShowPeakReadout)
            {
                DrawText(context, FormatDb(maximum), maximumBounds, fontSize, new SolidColorBrush(Color.FromRgb(255, 190, 90)), TextAlignment.Center);
                DrawText(context, FormatDb(currentPeak), currentBounds, fontSize, Brushes.White, TextAlignment.Center);
            }
            if (meters.ShowClipIndicator)
            {
                context.DrawRectangle(overload ? ColorBrush(meters.ClipColor) : new SolidColorBrush(Color.FromRgb(74, 15, 15)), null, overloadBounds);
            }
            context.DrawRectangle(Brushes.Black, new Pen(Brushes.DimGray, 1), meter);
            var peakFill = MeterRailLayout.CalculateFillRatio(display.Peak, meters.DisplayRangeDb) * meter.Height;
            context.DrawRectangle(ColorBrush(meters.PeakColor), null, new Rect(meter.Left + 1, meter.Bottom - peakFill, meter.Width - 2, peakFill));
            if (meters.ShowRmsBars)
            {
                var rmsFill = MeterRailLayout.CalculateFillRatio(display.Rms, meters.DisplayRangeDb) * meter.Height;
                var rmsWidth = Math.Max(2, (meter.Width - 2) / 3);
                context.DrawRectangle(ColorBrush(meters.RmsColor), null, new Rect(meter.Left + (meter.Width - rmsWidth) / 2, meter.Bottom - rmsFill, rmsWidth, rmsFill));
            }
            var holdY = meter.Bottom - (MeterRailLayout.CalculateFillRatio(display.PeakHold, meters.DisplayRangeDb) * meter.Height);
            context.DrawLine(new Pen(Brushes.White, 1), new Point(meter.Left + 1, holdY), new Point(meter.Right - 1, holdY));
            if (meters.ShowDbScale)
            {
                foreach (var tick in AxisTicks.PeakRailDb(meters.DisplayRangeDb))
                {
                    var y = meter.Bottom - (tick.Ratio * meter.Height);
                    context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), 0.5), new Point(meter.Left, y), new Point(meter.Right, y));
                }
            }
            var labelBounds = new Rect(meter.Left, layout.ChannelLabels.Top, meter.Width, layout.ChannelLabels.Height);
            DrawText(context, channel == 0 ? "L" : "R", labelBounds, fontSize, Brushes.LightGray, TextAlignment.Center);
        }
        if (meters.ShowLufsIndicator)
        {
            var column = layout.LufsMeter;
            context.DrawRectangle(Brushes.Black, new Pen(Brushes.DimGray, 1), column);
            if (lufsValue.HasValue)
            {
                var fill = MeterRailLayout.CalculateLufsRatio(lufsValue.Value, lufsScale) * column.Height;
                context.DrawRectangle(lufsBrush, null, new Rect(column.Left + 1, column.Bottom - fill, column.Width - 2, fill));
            }
            foreach (var tick in AxisTicks.LufsLabels(lufsScale))
            {
                var y = column.Bottom - (tick.Ratio * column.Height);
                context.DrawLine(new Pen(Brushes.LightGray, 0.5), new Point(column.Right, y), new Point(column.Right + 3, y));
                DrawText(context, tick.Label, new Rect(layout.LufsScale.Left + 3, y - 6, layout.LufsScale.Width - 3, 12), fontSize - 1, dimText, TextAlignment.Left);
            }
            DrawText(context, "LU", new Rect(column.Left - 4, layout.ChannelLabels.Top, column.Width + 8, layout.ChannelLabels.Height), fontSize, lufsBrush, TextAlignment.Center);
        }
        if (meters.ShowLkfsReadout)
        {
            DrawText(context, lufsValue.HasValue ? $"{lufsValue.Value:0.0}" : "—", layout.LufsReadout, fontSize + 5, lufsBrush, TextAlignment.Center, FontWeights.Bold);
            DrawText(context, "LUFS", layout.LufsCaption, fontSize, dimText, TextAlignment.Center);
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

    private void DrawText(DrawingContext context, string text, Rect bounds, double size, Brush brush, TextAlignment alignment, FontWeight? weight = null)
    {
        var formatted = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var x = alignment switch
        {
            TextAlignment.Right => bounds.Right - formatted.Width,
            TextAlignment.Left => bounds.Left,
            _ => bounds.Left + ((bounds.Width - formatted.Width) / 2),
        };
        context.DrawText(formatted, new Point(x, bounds.Top + ((bounds.Height - formatted.Height) / 2)));
    }

    private static string FormatDb(float value) =>
        (!float.IsFinite(value) || value <= 0 ? -120d : 20 * Math.Log10(value)) is var db && db <= -99.95 ? "-∞" : db.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    private static Brush ColorBrush(string color) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}

public enum MeterRailResetTarget { Maximum, Overload }

public sealed class MeterRailResetEventArgs(int channel, MeterRailResetTarget target) : EventArgs
{
    public int Channel { get; } = channel;
    public MeterRailResetTarget Target { get; } = target;
}
