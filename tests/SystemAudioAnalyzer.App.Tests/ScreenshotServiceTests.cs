using SystemAudioAnalyzer.App.Services;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class ScreenshotServiceTests
{
    [Fact]
    public void ScreenshotPathUsesPaneNameAndTimestampUnderScreenshotsDirectory()
    {
        var timestamp = new DateTimeOffset(2026, 10, 7, 12, 34, 56, TimeSpan.Zero);

        var path = ScreenshotService.CreateFilePath("C:\\AAAnalyzer", "Waterfall L/R", timestamp);

        Assert.Equal(Path.Combine("C:\\AAAnalyzer", "Screenshots", "Waterfall-L-R_20261007_123456.png"), path);
    }
}
