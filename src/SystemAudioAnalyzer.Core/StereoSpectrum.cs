namespace SystemAudioAnalyzer.Core;

public sealed class StereoSpectrum(Spectrum mono, Spectrum left, Spectrum right)
{
    public Spectrum Mono { get; } = mono ?? throw new ArgumentNullException(nameof(mono));

    public Spectrum Left { get; } = left ?? throw new ArgumentNullException(nameof(left));

    public Spectrum Right { get; } = right ?? throw new ArgumentNullException(nameof(right));
}
