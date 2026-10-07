namespace SystemAudioAnalyzer.Core.Tests;

public sealed class SpectrumAnalyzerTests
{
    // A bin-centred full-scale sine reads 1.0 (0 dBFS); the neighbouring bin shows the
    // window's main-lobe leakage, which tells the windows apart: (a1 / 2) / a0.
    [Theory]
    [InlineData(SpectrumWindow.Rectangular, 0f)]
    [InlineData(SpectrumWindow.Hann, 0.5f)]
    [InlineData(SpectrumWindow.Hamming, 0.426f)]
    [InlineData(SpectrumWindow.Blackman, 0.595f)]
    public void SelectedWindowAppliesToMonoAndBothStereoChannels(SpectrumWindow window, float expectedNeighbour)
    {
        const int fftSize = 256;
        const int bin = 32;
        var analyzer = new SpectrumAnalyzer(fftSize, window);
        var samples = new float[fftSize * 2];
        for (var index = 0; index < fftSize; index++)
        {
            var value = MathF.Sin(2 * MathF.PI * bin * index / fftSize);
            samples[index * 2] = value;
            samples[(index * 2) + 1] = value;
        }

        Assert.True(analyzer.TryProcessStereo(samples, new AudioFormat(48_000, 2), out var spectrum));
        Assert.NotNull(spectrum);
        foreach (var channel in new[] { spectrum.Mono, spectrum.Left, spectrum.Right })
        {
            Assert.InRange(channel.Magnitudes[bin], 0.97f, 1.03f);
            Assert.InRange(channel.Magnitudes[bin + 1], expectedNeighbour - 0.06f, expectedNeighbour + 0.06f);
        }
    }

    [Fact]
    public void ConstantSignalReadsItsAmplitudeAtDcForEveryWindow()
    {
        foreach (var window in Enum.GetValues<SpectrumWindow>())
        {
            var analyzer = new SpectrumAnalyzer(64, window);
            Assert.True(analyzer.TryProcess(Enumerable.Repeat(0.5f, 64).ToArray(), new AudioFormat(48_000, 1), out var spectrum));
            Assert.InRange(spectrum!.Magnitudes[0], 0.499f, 0.501f);
        }
    }

    [Fact]
    public void DirectConstructorDefaultsToHannForSmallUtilityFfts()
    {
        var analyzer = new SpectrumAnalyzer(64);
        Assert.True(analyzer.TryProcess(Enumerable.Repeat(1f, 64).ToArray(), new AudioFormat(48_000, 1), out var spectrum));
        Assert.InRange(spectrum!.Magnitudes[0], 0.999f, 1.001f);
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

    [Fact]
    public void LargeBuffersAreDrainedSoTheLatestWindowIsReturned()
    {
        const int fftSize = 64;
        const int bin = 8;
        var analyzer = new SpectrumAnalyzer(fftSize, SpectrumWindow.Rectangular);
        var samples = new float[4_096];
        for (var index = 3_000; index < samples.Length; index++)
        {
            samples[index] = MathF.Sin(2 * MathF.PI * bin * index / fftSize);
        }

        Assert.True(analyzer.TryProcess(samples, new AudioFormat(48_000, 1), out var spectrum));

        Assert.InRange(spectrum!.Magnitudes[bin], 0.97f, 1.03f);
    }

    [Fact]
    public void StereoProcessingAlsoReturnsTheLatestWindowOfALargeBuffer()
    {
        const int fftSize = 64;
        const int bin = 8;
        var analyzer = new SpectrumAnalyzer(fftSize, SpectrumWindow.Rectangular);
        var samples = new float[4_096 * 2];
        for (var index = 3_000; index < 4_096; index++)
        {
            samples[index * 2] = MathF.Sin(2 * MathF.PI * bin * index / fftSize);
        }

        Assert.True(analyzer.TryProcessStereo(samples, new AudioFormat(48_000, 2), out var spectrum));

        Assert.InRange(spectrum!.Left.Magnitudes[bin], 0.97f, 1.03f);
        Assert.InRange(spectrum.Right.Magnitudes.Max(), 0f, 0.0001f);
        Assert.InRange(spectrum.Mono.Magnitudes[bin], 0.47f, 0.53f);
    }
}
