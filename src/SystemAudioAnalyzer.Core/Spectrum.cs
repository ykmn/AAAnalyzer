using System.Collections.ObjectModel;

namespace SystemAudioAnalyzer.Core;

public sealed class Spectrum
{
    public Spectrum(int sampleRate, int fftSize, IEnumerable<float> magnitudes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fftSize);
        ArgumentNullException.ThrowIfNull(magnitudes);

        SampleRate = sampleRate;
        FftSize = fftSize;
        Magnitudes = new ReadOnlyCollection<float>(magnitudes.ToArray());
    }

    public int SampleRate { get; }

    public int FftSize { get; }

    public IReadOnlyList<float> Magnitudes { get; }

    public float BinWidthHz => (float)SampleRate / FftSize;

    public float GetFrequencyHz(int binIndex)
    {
        if (binIndex < 0 || binIndex >= Magnitudes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(binIndex));
        }

        return binIndex * BinWidthHz;
    }
}
