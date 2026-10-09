using SystemAudioAnalyzer.App.Rendering;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class WaterfallBitmapBufferTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private const uint Black = 0xFF000000;

    private static uint[] Row(uint color, int width = 3) => Enumerable.Repeat(color, width).ToArray();

    private static uint RowColor(WaterfallBitmapBuffer buffer, int line) => buffer.Pixels[line * buffer.Width];

    [Fact]
    public void FirstRowIsWrittenAtTheTop()
    {
        var buffer = new WaterfallBitmapBuffer(3, 10, TimeSpan.FromSeconds(10), Black);

        buffer.Append(Start, Row(0xFF111111));

        Assert.Equal(0xFF111111u, RowColor(buffer, 0));
        Assert.Equal(Black, RowColor(buffer, 1));
    }

    [Fact]
    public void NewerRowsEnterAtTheTopAndOlderRowsMoveDown()
    {
        var buffer = new WaterfallBitmapBuffer(3, 10, TimeSpan.FromSeconds(10), Black);

        buffer.Append(Start, Row(0xFF111111));
        var scrolled = buffer.Append(Start.AddSeconds(1), Row(0xFF222222));

        Assert.True(scrolled);
        Assert.Equal(0xFF222222u, RowColor(buffer, 0));
        Assert.Equal(0xFF111111u, RowColor(buffer, 1));
    }

    [Fact]
    public void FramesFasterThanOnePixelOnlyRefreshTheTopRow()
    {
        var buffer = new WaterfallBitmapBuffer(3, 10, TimeSpan.FromSeconds(10), Black);
        buffer.Append(Start, Row(0xFF111111));

        var scrolled = buffer.Append(Start.AddMilliseconds(10), Row(0xFF222222));

        Assert.False(scrolled);
        Assert.Equal(0xFF222222u, RowColor(buffer, 0));
        Assert.Equal(Black, RowColor(buffer, 1));
    }

    [Fact]
    public void FractionalPixelsAccumulateAcrossFrames()
    {
        var buffer = new WaterfallBitmapBuffer(3, 10, TimeSpan.FromSeconds(10), Black);
        buffer.Append(Start, Row(0xFF111111));

        var first = buffer.Append(Start.AddMilliseconds(600), Row(0xFF222222));
        var second = buffer.Append(Start.AddMilliseconds(1_200), Row(0xFF333333));

        Assert.False(first);
        Assert.True(second);
        Assert.Equal(0xFF333333u, RowColor(buffer, 0));
        Assert.Equal(0xFF222222u, RowColor(buffer, 1));
    }

    [Fact]
    public void LongGapsLeaveBackgroundBelowTheLatestRow()
    {
        var buffer = new WaterfallBitmapBuffer(3, 4, TimeSpan.FromSeconds(10), Black);
        buffer.Append(Start, Row(0xFF111111));

        buffer.Append(Start.AddSeconds(60), Row(0xFF222222));

        Assert.Equal(0xFF222222u, RowColor(buffer, 0));
        Assert.All(Enumerable.Range(1, 3), line => Assert.Equal(Black, RowColor(buffer, line)));
    }

    [Fact]
    public void ClearRestoresBackgroundAndRestartsTiming()
    {
        var buffer = new WaterfallBitmapBuffer(3, 4, TimeSpan.FromSeconds(10), Black);
        buffer.Append(Start, Row(0xFF111111));

        buffer.Clear();

        Assert.All(buffer.Pixels, pixel => Assert.Equal(Black, pixel));
        Assert.True(buffer.Append(Start.AddMilliseconds(1), Row(0xFF222222)));
    }

    [Fact]
    public void RowsWithTheWrongWidthAreRejected()
    {
        var buffer = new WaterfallBitmapBuffer(3, 4, TimeSpan.FromSeconds(10), Black);

        Assert.Throws<ArgumentException>(() => buffer.Append(Start, Row(0xFF111111, 2)));
    }
}
