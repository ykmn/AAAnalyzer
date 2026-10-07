namespace SystemAudioAnalyzer.Core;

public enum SpectrumWindow
{
    Rectangular,
    Hann,
    Hamming,
    Blackman,
}

public sealed record AnalysisConfiguration
{
    public AnalysisConfiguration(int FftSize, SpectrumWindow Window)
    {
        if (FftSize < 512 || FftSize > 16384 || (FftSize & (FftSize - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(FftSize), "FFT size must be a power of two between 512 and 16384.");
        }

        if (!Enum.IsDefined(Window))
        {
            throw new ArgumentOutOfRangeException(nameof(Window));
        }

        this.FftSize = FftSize;
        this.Window = Window;
    }

    public int FftSize { get; }

    public SpectrumWindow Window { get; }
}
