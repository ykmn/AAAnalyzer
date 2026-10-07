namespace SystemAudioAnalyzer.Core.Tests;

public sealed class SpectrumAnalyzerTests
{
    // A constant signal's DC magnitude is the hand-derived sum of the window.
    [Theory]
    [InlineData(SpectrumWindow.Rectangular, 64f)]
    [InlineData(SpectrumWindow.Hann, 31.5f)]
    [InlineData(SpectrumWindow.Hamming, 34.1f)]
    [InlineData(SpectrumWindow.Blackman, 26.46f)]
    public void SelectedWindowAppliesToMonoAndBothStereoChannels(SpectrumWindow window, float expectedDc)
    {
        var analyzer = new SpectrumAnalyzer(64, window);
        var samples = Enumerable.Repeat(1f, 128).ToArray();

        Assert.True(analyzer.TryProcessStereo(samples, new AudioFormat(48_000, 2), out var spectrum));
        Assert.NotNull(spectrum);
        foreach (var channel in new[] { spectrum.Mono, spectrum.Left, spectrum.Right })
        {
            Assert.InRange(channel.Magnitudes[0], expectedDc - 0.001f, expectedDc + 0.001f);
        }

        var mono = new SpectrumAnalyzer(64, window);
        Assert.True(mono.TryProcess(samples.AsSpan(0, 64), new AudioFormat(48_000, 1), out var monoSpectrum));
        Assert.InRange(monoSpectrum!.Magnitudes[0], expectedDc - 0.001f, expectedDc + 0.001f);
    }

    [Fact]
    public void DirectConstructorDefaultsToHannForSmallUtilityFfts()
    {
        var analyzer = new SpectrumAnalyzer(64);
        Assert.True(analyzer.TryProcess(Enumerable.Repeat(1f, 64).ToArray(), new AudioFormat(48_000, 1), out var spectrum));
        Assert.InRange(spectrum!.Magnitudes[0], 31.499f, 31.501f);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    [InlineData(8192)]
    [InlineData(16384)]
    public void ConfigurationAcceptsSelectableFftSizes(int fftSize)
    {
        var configuration = new AnalysisConfiguration(fftSize, SpectrumWindow.Hann);
        var analyzer = new SpectrumAnalyzer(configuration.FftSize, configuration.Window);
        Assert.True(analyzer.TryProcess(new float[fftSize], new AudioFormat(48_000, 1), out var spectrum));
        Assert.Equal(fftSize, spectrum!.FftSize);
        Assert.Equal(fftSize / 2 + 1, spectrum.Magnitudes.Count);
    }

    [Theory]
    [InlineData(-512)]
    [InlineData(0)]
    [InlineData(64)]
    [InlineData(511)]
    [InlineData(513)]
    [InlineData(1000)]
    [InlineData(16385)]
    [InlineData(32768)]
    public void ConfigurationRejectsInvalidSelectableFftSizes(int fftSize) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnalysisConfiguration(fftSize, SpectrumWindow.Hann));

    [Fact]
    public void UndefinedWindowsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnalysisConfiguration(512, (SpectrumWindow)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpectrumAnalyzer(64, (SpectrumWindow)999));
    }

    [Fact]
    public void LeftOnlyToneAppearsOnlyInLeftSpectrum()
    {
        var analyzer = new SpectrumAnalyzer(fftSize: 64);
        var format = new AudioFormat(48_000, channels: 2);
        var samples = new float[64 * 2];
        for (var frame = 0; frame < 64; frame++)
        {
            samples[frame * 2] = MathF.Sin((2f * MathF.PI * 6 * frame) / 64);
        }

        var produced = analyzer.TryProcessStereo(samples, format, out var spectrum);

        Assert.True(produced);
        Assert.NotNull(spectrum);
        Assert.True(spectrum.Left.Magnitudes.Max() > 0.1f);
        Assert.InRange(spectrum.Right.Magnitudes.Max(), 0f, 0.0001f);
    }

    [Fact]
    public void ProcessPlacesASineWaveInItsExpectedFrequencyBin()
    {
        const int fftSize = 4096;
        const int sampleRate = 48_000;
        const int targetBin = 32;
        var samples = Enumerable.Range(0, fftSize)
            .Select(index => MathF.Sin(2 * MathF.PI * targetBin * index / fftSize))
            .ToArray();
        var analyzer = new SpectrumAnalyzer(fftSize);

        var hasSpectrum = analyzer.TryProcess(samples, new AudioFormat(sampleRate, channels: 1), out var spectrum);

        Assert.True(hasSpectrum);
        var strongestBin = spectrum!.Magnitudes
            .Select((magnitude, index) => (magnitude, index))
            .MaxBy(item => item.magnitude)
            .index;
        Assert.Equal(targetBin, strongestBin);
        Assert.Equal(375f, spectrum.GetFrequencyHz(targetBin));
    }

    [Fact]
    public void ProcessWaitsForACompleteAnalysisWindow()
    {
        var analyzer = new SpectrumAnalyzer(8);

        var hasSpectrum = analyzer.TryProcess(new float[7], new AudioFormat(48_000, channels: 1), out var spectrum);

        Assert.False(hasSpectrum);
        Assert.Null(spectrum);
    }
}
