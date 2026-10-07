namespace SystemAudioAnalyzer.Core;

public sealed class AdvancedMeasurementFrame(
    StereoTruePeakMeasurement truePeak,
    LoudnessMeasurement loudness,
    StereoSpectrum? stereoSpectrum = null,
    PhaseScopeFrame? phaseScope = null)
{
    public StereoTruePeakMeasurement TruePeak { get; } = truePeak ?? throw new ArgumentNullException(nameof(truePeak));

    public LoudnessMeasurement Loudness { get; } = loudness ?? throw new ArgumentNullException(nameof(loudness));

    public StereoSpectrum? StereoSpectrum { get; } = stereoSpectrum;

    public PhaseScopeFrame? PhaseScope { get; } = phaseScope;
}
