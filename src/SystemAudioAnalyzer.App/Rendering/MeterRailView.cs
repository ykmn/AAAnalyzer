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
    public static readonly DependencyProperty LoudnessRangeProperty = DependencyProperty.Register(
        nameof(LoudnessRange), typeof((double Minimum, double Maximum)?), typeof(MeterRailView), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    private double? _phaseCorrelation;
    private double _phaseMinimum = 1;
    private readonly MeteringHistory[] _meteringHistories = [new(), new()];
    private readonly MeterDisplayState[] _displayStates = [new(0, 0, 0), new(0, 0, 0)];

    public AnalysisFrame? Frame
    {
        get => (AnalysisFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public MeasurementSettings Settings { get => (MeasurementSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }

    /// <summary>The range the Loudness plot currently shows, so the LU bar lines up with it when that range is automatic.</summary>
    public (double Minimum, double Maximum)? LoudnessRange { get => ((double, double)?)GetValue(LoudnessRangeProperty); set => SetValue(LoudnessRangeProperty, value); }

    public MeterRailView() => Localization.Localizer.Instance.LanguageChanged += (_, _) => InvalidateVisual();

    public event EventHandler<MeterRailResetEventArgs>? ResetRequested;

    public void ResetPhase()
    {
        _phaseCorrelation = null;
        _phaseMinimum = 1;
        InvalidateVisual();
    }

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
        var lufsScale = MeterRailLayout.ResolveLufsRange(Settings, LoudnessRange);
        var lufsBrush = ColorBrush(meters.LufsColor);
        var dimText = new SolidColorBrush(Color.FromRgb(120, 135, 150));
        if (meters.ShowPeakReadout)
        {
            DrawText(context, Localization.Localizer.T("RailMax"), new Rect(layout.DbScale.Left, layout.LeftMaximum.Top, layout.DbScale.Width, 12), fontSize - 1, new SolidColorBrush(Color.FromRgb(255, 190, 90)), TextAlignment.Right);
            DrawText(context, Localization.Localizer.T("RailNow"), new Rect(layout.DbScale.Left, layout.LeftCurrent.Top, layout.DbScale.Width, 12), fontSize - 1, dimText, TextAlignment.Right);
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
            var target = Settings.Loudness.TargetLufs;
            var bandTop = column.Bottom - (MeterRailLayout.CalculateLufsRatio(target + Settings.Loudness.TargetRangeLu, lufsScale) * column.Height);
            var bandBottom = column.Bottom - (MeterRailLayout.CalculateLufsRatio(target - Settings.Loudness.TargetRangeLu, lufsScale) * column.Height);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(110, 255, 138, 128)), null, new Rect(column.Left + 1, bandTop, column.Width - 2, Math.Max(0, bandBottom - bandTop)));
            if (target >= lufsScale.BottomDb && target <= lufsScale.TopDb)
            {
                var targetY = column.Bottom - (MeterRailLayout.CalculateLufsRatio(target, lufsScale) * column.Height);
                context.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(255, 138, 128)), 1.5), new Point(column.Left - 2, targetY), new Point(column.Right + 3, targetY));
            }
            DrawText(context, "LU", new Rect(column.Left - 4, layout.ChannelLabels.Top, column.Width + 8, layout.ChannelLabels.Height), fontSize, lufsBrush, TextAlignment.Center);
        }
        DrawPhaseMeter(context, layout, fontSize, dimText);
        if (meters.ShowLkfsReadout)
        {
            DrawText(context, lufsValue.HasValue ? $"{lufsValue.Value:0.0}" : "—", layout.LufsReadout, fontSize + 5, lufsBrush, TextAlignment.Center, FontWeights.Bold);
            DrawText(context, MeterRailLayout.LufsCaptionText(meters.LufsMetric), layout.LufsCaption, fontSize, dimText, TextAlignment.Center);
        }
    }

    private void DrawPhaseMeter(DrawingContext context, MeterRailLayout layout, double fontSize, Brush dimText)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        var bar = layout.PhaseBar;
        double X(double value) => bar.Left + ((value + 1d) / 2d * bar.Width);
        context.DrawRectangle(Brushes.Black, new Pen(Brushes.DimGray, 1), bar);
        if (_phaseCorrelation is { } value)
        {
            var from = X(Math.Min(0d, value));
            var to = X(Math.Max(0d, value));
            Brush fill = value >= 0
                ? new LinearGradientBrush(Color.FromRgb(255, 214, 10), Color.FromRgb(255, 140, 0), 0d)
                : new LinearGradientBrush(Color.FromRgb(255, 140, 0), Color.FromRgb(230, 50, 40), 0d);
            context.DrawRectangle(fill, null, new Rect(from, bar.Top + 1, Math.Max(0d, to - from), bar.Height - 2));
        }
        // Scale -1 -0.5 0 0.5 1; each label is centred on its tick, kept inside the rail.
        foreach (var mark in new[] { -1d, -0.5, 0d, 0.5, 1d })
        {
            var x = X(mark);
            context.DrawLine(new Pen(Brushes.LightGray, 0.5), new Point(x, bar.Bottom), new Point(x, bar.Bottom + 3));
            var label = mark == 0 ? "0" : mark.ToString("0.#", culture);
            DrawText(context, label, new Rect(Math.Clamp(x - 14, layout.PhaseScale.Left, layout.PhaseScale.Right - 28), layout.PhaseScale.Top + 2, 28, 10), fontSize - 2, dimText, TextAlignment.Center);
        }
        // Left: lowest correlation since the last reset. Right: current.
        var minimumText = _phaseMinimum < 1 ? _phaseMinimum.ToString("0.0", culture) : "—";
        var currentText = _phaseCorrelation is { } current ? current.ToString("0.0", culture) : "—";
        DrawText(context, minimumText, layout.PhaseValues, fontSize - 1, new SolidColorBrush(Color.FromRgb(255, 190, 90)), TextAlignment.Left);
        DrawText(context, currentText, layout.PhaseValues, fontSize - 1, Brushes.White, TextAlignment.Right);
    }

    private static void OnFrameChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var view = (MeterRailView)target;
        var frame = args.NewValue as AnalysisFrame;
        var truePeak = frame?.AdvancedMeasurements?.TruePeak;
        if (MeterRailLayout.CalculateCorrelation(frame?.AdvancedMeasurements?.PhaseScope?.Points) is { } correlation)
        {
            // Light smoothing keeps the bar readable.
            view._phaseCorrelation = view._phaseCorrelation is { } previous ? previous + ((correlation - previous) * 0.2) : correlation;
            view._phaseMinimum = Math.Min(view._phaseMinimum, view._phaseCorrelation.Value);
        }
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
        (!float.IsFinite(value) || value <= 0 ? -120d : 20 * Math.Log10(value)) is var db && db <= -99.95 ? "-∞" : db.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

    private static Brush ColorBrush(string color) =>
        new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}

public enum MeterRailResetTarget { Maximum, Overload }

public sealed class MeterRailResetEventArgs(int channel, MeterRailResetTarget target) : EventArgs
{
    public int Channel { get; } = channel;
    public MeterRailResetTarget Target { get; } = target;
}
