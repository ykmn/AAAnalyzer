namespace SystemAudioAnalyzer.Core;

public sealed class AdvancedMeasurementFrame(StereoTruePeakMeasurement truePeak)
{
    public StereoTruePeakMeasurement TruePeak { get; } = truePeak ?? throw new ArgumentNullException(nameof(truePeak));
}
