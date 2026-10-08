using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Rendering;

/// <summary>Stereo waterfall: left channel above right, time flowing top to bottom, one bitmap per channel.</summary>
public sealed class WaterfallView : FrameworkElement
{
    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(nameof(Settings), typeof(MeasurementSettings), typeof(WaterfallView), new FrameworkPropertyMetadata(MeasurementSettings.Default, FrameworkPropertyMetadataOptions.AffectsRender, OnSettingsChanged));
    public static readonly DependencyProperty FrameProperty = DependencyProperty.Register(
        nameof(Frame), typeof(AnalysisFrame), typeof(WaterfallView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFrameChanged));

    private const uint Background = 0xFF000000;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);

    private WaterfallBitmapBuffer? _left;
    private WaterfallBitmapBuffer? _right;
    private WriteableBitmap? _leftBitmap;
    private WriteableBitmap? _rightBitmap;
    private WaterfallPixelSettings _pixels = WaterfallPixelSettings.From(MeasurementSettings.Default);
    private double _cursor = 0.5;

    public WaterfallView()
    {
        MouseMove += OnMouseMove;
        Localization.Localizer.Instance.LanguageChanged += (_, _) => InvalidateVisual();
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
    }

    public AnalysisFrame? Frame
    {
        get => (AnalysisFrame?)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public MeasurementSettings Settings { get => (MeasurementSettings)GetValue(SettingsProperty); set => SetValue(SettingsProperty, value); }

    public void Reset()
    {
        _left?.Clear();
        _right?.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(Brushes.Black, null, new Rect(new Point(), RenderSize));
        if (ActualWidth <= 1 || ActualHeight <= 1) return;
        var layout = WaterfallLayout.Calculate(ActualWidth, ActualHeight);
        EnsureBuffers(layout);
        if (_left is null || _right is null || _leftBitmap is null || _rightBitmap is null) return;
        Upload(_leftBitmap, _left);
        Upload(_rightBitmap, _right);
        context.DrawImage(_leftBitmap, layout.LeftBounds);
        context.DrawImage(_rightBitmap, layout.RightBounds);
        DrawCursor(context, layout);
        DrawAxis(context, layout);
    }

    private static void OnFrameChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is AnalysisFrame frame) ((WaterfallView)target).AppendFrame(frame);
    }

    private static void OnSettingsChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var view = (WaterfallView)target;
        view._pixels = WaterfallPixelSettings.From(view.Settings);
    }

    private void AppendFrame(AnalysisFrame frame)
    {
        var stereo = frame.AdvancedMeasurements?.StereoSpectrum;
        if (stereo is null || _left is null || _right is null) return;
        var sampleRate = frame.Format.SampleRate;
        _left.Append(frame.Timestamp, WaterfallRowPixelizer.CreateRow(stereo.Left.Magnitudes, sampleRate, stereo.Left.FftSize, _left.Width, _pixels));
        _right.Append(frame.Timestamp, WaterfallRowPixelizer.CreateRow(stereo.Right.Magnitudes, sampleRate, stereo.Right.FftSize, _right.Width, _pixels));
    }

    private void EnsureBuffers(WaterfallLayout layout)
    {
        var width = Math.Max(1, (int)Math.Round(layout.LeftBounds.Width));
        var height = Math.Max(1, (int)Math.Round(layout.LeftBounds.Height));
        if (_left is not null && _left.Width == width && _left.Height == height) return;
        _left = new WaterfallBitmapBuffer(width, height, Window, Background);
        _right = new WaterfallBitmapBuffer(width, height, Window, Background);
        _leftBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _rightBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
    }

    private static void Upload(WriteableBitmap bitmap, WaterfallBitmapBuffer buffer) =>
        bitmap.WritePixels(new Int32Rect(0, 0, buffer.Width, buffer.Height), buffer.Pixels, buffer.Width * 4, 0);

    private void OnMouseMove(object sender, MouseEventArgs args)
    {
        if (ActualWidth <= 1 || ActualHeight <= 1) return;
        _cursor = WaterfallLayout.Calculate(ActualWidth, ActualHeight).GetNormalizedX(args.GetPosition(this).X);
        InvalidateVisual();
    }

    private void DrawCursor(DrawingContext context, WaterfallLayout layout)
    {
        var x = layout.LeftBounds.Left + (layout.LeftBounds.Width * _cursor);
        context.DrawLine(new Pen(ColorBrush(Settings.Analyzer.CursorColor), 1), new Point(x, layout.LeftBounds.Top), new Point(x, layout.RightBounds.Bottom));
        var hertz = Math.Clamp(FrequencyScale.ToHertz(_cursor, Settings.Analyzer.FrequencyScale), FrequencyScale.MinimumHertz, FrequencyScale.MaximumHertz);
        var text = Format(FrequencyScale.Format(hertz), 11, Brushes.White);
        var plaque = new Rect(Math.Clamp(x - text.Width - 8, 0, Math.Max(0, ActualWidth - text.Width - 8)), 3, text.Width + 8, text.Height + 2);
        context.DrawRectangle(Brushes.Black, null, plaque);
        context.DrawText(text, new Point(plaque.Left + 4, plaque.Top + 1));
    }

    private void DrawAxis(DrawingContext context, WaterfallLayout layout)
    {
        var axis = layout.AxisBounds;
        var textBrush = ColorBrush(Settings.Analyzer.TextColor);
        var lastRight = double.NegativeInfinity;
        foreach (var label in AxisTicks.WaterfallFrequencyLabels())
        {
            var x = FrequencyScale.ToNormalized(label.Hertz, Settings.Analyzer.FrequencyScale) * axis.Width;
            context.DrawLine(new Pen(Brushes.DimGray, 1), new Point(x, axis.Top), new Point(x, axis.Top + 4));
            var text = Format(label.Label, 10, textBrush);
            var left = Math.Clamp(x - (text.Width / 2), 1, Math.Max(1, axis.Width - text.Width - 1));
            if (left < lastRight + 6) continue;
            context.DrawText(text, new Point(left, axis.Top + 4));
            lastRight = left + text.Width;
        }
    }

    private FormattedText Format(string text, double size, Brush brush) =>
        new(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static Brush ColorBrush(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}
