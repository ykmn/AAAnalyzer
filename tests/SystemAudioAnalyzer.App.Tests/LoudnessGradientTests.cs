using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.App.Settings;
using System.Windows.Media;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class LoudnessGradientTests
{
    [Fact]
    public void DefaultGradientMatchesReferenceLufsStopsExactly()
    {
        var stops = MeasurementSettings.Default.Loudness.GradientStops;

        Assert.Equal(new[] { -15d, -12d, -8d, -6d }, stops.Select(stop => stop.LevelDb));
        Assert.Equal(new[] { "#2F6FD6", "#2FA84F", "#E0C93A", "#D6392F" }, stops.Select(stop => stop.Color));
    }

    [Fact]
    public void LoudnessGradientInterpolatesBetweenReferenceStopsAndClampsEnds()
    {
        var stops = MeasurementSettings.Default.Loudness.GradientStops;

        Assert.Equal(Color.FromRgb(47, 140, 147), ColorGradient.Sample(stops, -13.5));
        Assert.Equal((Color)ColorConverter.ConvertFromString("#2F6FD6"), ColorGradient.Sample(stops, -20));
        Assert.Equal((Color)ColorConverter.ConvertFromString("#D6392F"), ColorGradient.Sample(stops, 0));
    }

    [Fact]
    public void ManualLoudnessScaleUsesConfiguredCentreAndSpanExactly()
    {
        var settings = MeasurementSettings.Default.Loudness with { AutoScale = false, CentreLufs = -18, SpanLufs = 12 };

        Assert.Equal((-24d, -12d), LoudnessDisplayScale.ResolveRange(settings, [-40, -10]));
    }

    [Fact]
    public void AutomaticLoudnessScaleUsesSelectedMetricValues()
    {
        var point = new LoudnessHistoryPoint(DateTimeOffset.UtcNow, -18, -24, -30);

        Assert.Equal(-18f, LoudnessDisplayScale.SelectMetric(point, LoudnessMetric.Momentary));
        Assert.Equal(-24f, LoudnessDisplayScale.SelectMetric(point, LoudnessMetric.ShortTerm));
        Assert.Equal(-30f, LoudnessDisplayScale.SelectMetric(point, LoudnessMetric.Integrated));
        Assert.Equal((-25d, -17d), LoudnessDisplayScale.ResolveRange(MeasurementSettings.Default.Loudness, [-24, -18]));
    }
}
