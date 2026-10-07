using System.Windows.Media;

namespace SystemAudioAnalyzer.App.Rendering;

using SystemAudioAnalyzer.App.Settings;

public static class RtaRenderer
{
    public static void Render(DrawingContext context, AnalysisFrame frame, Rect bounds, AnalyzerSettings? settings = null)
    {
        var spectrum = frame.Spectrum;
        if (spectrum is null || spectrum.Magnitudes.Count < 2 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        settings ??= MeasurementSettings.Default.Analyzer;

        var geometry = new StreamGeometry();
        using (var drawing = geometry.Open())
        {
            for (var index = 1; index < spectrum.Magnitudes.Count; index++)
            {
                var frequency = Math.Clamp(spectrum.GetFrequencyHz(index), FrequencyScale.MinimumHertz, FrequencyScale.MaximumHertz);
                var normalizedFrequency = FrequencyScale.ToNormalized(frequency, settings.FrequencyScale);
                var magnitude = SpectrumDisplayScale.ToNormalizedAmplitude(spectrum.Magnitudes[index], settings.Gain, settings.AmplitudeScale, settings.DisplayFloorDb);
                var point = new Point(
                    bounds.Left + (normalizedFrequency * bounds.Width),
                    bounds.Bottom - (magnitude * bounds.Height));
                if (index == 1)
                {
                    drawing.BeginFigure(point, false, false);
                }
                else
                {
                    drawing.LineTo(point, true, false);
                }
            }
        }

        geometry.Freeze();
        var color = (Color)ColorConverter.ConvertFromString(settings.TextColor);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        context.DrawGeometry(null, new Pen(brush, 1.5), geometry);
    }
}
