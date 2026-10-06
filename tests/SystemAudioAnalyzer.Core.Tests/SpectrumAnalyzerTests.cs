namespace SystemAudioAnalyzer.Core.Tests;

public sealed class SpectrumAnalyzerTests
{
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
