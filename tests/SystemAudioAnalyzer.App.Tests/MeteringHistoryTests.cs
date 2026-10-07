using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class MeteringHistoryTests
{
    [Fact]
    public void WaterfallHistoryUsesIndependentStereoSpectraFromTheFrame()
    {
        var history = new WaterfallHistory(TimeSpan.FromSeconds(10));
        var frame = CreateStereoFrame(new[] { 0.1f, 0.2f }, new[] { 0.7f, 0.8f });

        history.Append(frame);

        var row = Assert.Single(history.GetVisibleRows(frame.Timestamp));
        Assert.Equal(new[] { 0.1f, 0.2f }, row.Left);
        Assert.Equal(new[] { 0.7f, 0.8f }, row.Right);
    }

    [Fact]
    public void WaterfallHistoryKeepsOnlyTheLatestTenSeconds()
    {
        var history = new WaterfallHistory(TimeSpan.FromSeconds(10));
        var origin = DateTimeOffset.Parse("2026-10-07T12:00:00Z");

        history.Append(origin, new[] { 0.1f }, new[] { 0.2f });
        history.Append(origin.AddSeconds(10), new[] { 0.3f }, new[] { 0.4f });
        history.Append(origin.AddSeconds(11), new[] { 0.5f }, new[] { 0.6f });

        var rows = history.GetVisibleRows(origin.AddSeconds(11));

        Assert.Equal(2, rows.Count);
        Assert.Equal(origin.AddSeconds(10), rows[0].Timestamp);
        Assert.Equal(origin.AddSeconds(11), rows[1].Timestamp);
    }

    [Fact]
    public void LoudnessHistoryKeepsOnlyTheLatestMinute()
    {
        var history = new LoudnessHistory(TimeSpan.FromMinutes(1));
        var origin = DateTimeOffset.Parse("2026-10-07T12:00:00Z");

        history.Append(origin, -20f);
        history.Append(origin.AddMinutes(1), -18f);
        history.Append(origin.AddMinutes(1).AddSeconds(1), -16f);

        var points = history.GetVisiblePoints(origin.AddMinutes(1).AddSeconds(1));

        Assert.Equal(2, points.Count);
        Assert.Equal(origin.AddMinutes(1), points[0].Timestamp);
        Assert.Equal(-16f, points[1].Lufs);
    }

    [Fact]
    public void LoudnessHistoryRetainsAllThreeEbuMeasurements()
    {
        var history = new LoudnessHistory(TimeSpan.FromMinutes(1));
        var timestamp = DateTimeOffset.Parse("2026-10-07T12:00:00Z");

        history.Append(timestamp, new LoudnessMeasurement(-20f, -21f, -22f));

        var point = Assert.Single(history.GetVisiblePoints(timestamp));
        Assert.Equal(-20f, point.MomentaryLufs);
        Assert.Equal(-21f, point.ShortTermLufs);
        Assert.Equal(-22f, point.IntegratedLufs);
    }

    private static AnalysisFrame CreateStereoFrame(float[] left, float[] right)
    {
        var format = new AudioFormat(48_000, 2);
        var mono = new Spectrum(48_000, 4, new[] { 0.4f, 0.5f });
        var stereo = new StereoSpectrum(
            mono,
            new Spectrum(48_000, 4, left),
            new Spectrum(48_000, 4, right));
        var advanced = new AdvancedMeasurementFrame(
            new StereoTruePeakMeasurement(new[] { 0f, 0f }, new[] { 0f, 0f }, new[] { false, false }),
            new LoudnessMeasurement(null, null, null),
            stereo);

        return new AnalysisFrame(DateTimeOffset.Parse("2026-10-07T12:00:00Z"), format, [], mono, advancedMeasurements: advanced);
    }
}
