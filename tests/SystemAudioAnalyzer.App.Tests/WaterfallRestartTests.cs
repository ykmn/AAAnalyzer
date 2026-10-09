using System.Reflection;
using System.Windows;
using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class WaterfallRestartTests
{
    [Fact]
    public void RestartBreakSurvivesFramesThatHaveNoSpectrumYet()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { AssertBreak(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null) throw new Xunit.Sdk.XunitException(failure.ToString());
    }

    private static void AssertBreak()
    {
        var view = new WaterfallView { Width = 400, Height = 300 };
        view.Measure(new Size(400, 300));
        view.Arrange(new Rect(0, 0, 400, 300));
        view.UpdateLayout();

        var start = DateTimeOffset.UtcNow;
        view.Frame = WithSpectrum(start);
        // The restart: its first frame is flagged but still has no spectrum, the next one does and is 0.8 s later.
        view.Frame = new AnalysisFrame(start.AddMilliseconds(700), new AudioFormat(48_000, 2), [], null, isDiscontinuity: true);
        view.Frame = WithSpectrum(start.AddMilliseconds(800));

        var left = (WaterfallBitmapBuffer)typeof(WaterfallView).GetField("_left", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(view)!;
        var background = left.Pixels[(left.Height - 1) * left.Width];
        // 0.8 s of the default 20 s window on 140 lines is 5 lines: the new row, four background lines, the old row.
        Assert.NotEqual(background, left.Pixels[0]);
        Assert.All(Enumerable.Range(1, 4), line => Assert.Equal(background, left.Pixels[line * left.Width]));
        Assert.NotEqual(background, left.Pixels[5 * left.Width]);
    }

    private static AnalysisFrame WithSpectrum(DateTimeOffset timestamp)
    {
        var magnitudes = Enumerable.Repeat(0.3f, 2048).ToArray();
        Spectrum Make() => new(48_000, 4096, magnitudes);
        var advanced = new AdvancedMeasurementFrame(
            new StereoTruePeakMeasurement(new[] { 0f, 0f }, new[] { 0f, 0f }, new[] { false, false }),
            new LoudnessMeasurement(null, null, null),
            new StereoSpectrum(Make(), Make(), Make()));
        return new AnalysisFrame(timestamp, new AudioFormat(48_000, 2), [], Make(), advancedMeasurements: advanced);
    }
}
