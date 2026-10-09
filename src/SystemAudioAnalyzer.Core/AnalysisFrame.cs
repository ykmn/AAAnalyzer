using System.Collections.ObjectModel;

namespace SystemAudioAnalyzer.Core;

public sealed class AnalysisFrame
{
    public AnalysisFrame(
        DateTimeOffset timestamp,
        AudioFormat format,
        IEnumerable<ChannelLevel> levels,
        Spectrum? spectrum,
        long droppedBufferCount = 0,
        AdvancedMeasurementFrame? advancedMeasurements = null,
        bool isDiscontinuity = false)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(levels);

        Timestamp = timestamp;
        Format = format;
        Levels = new ReadOnlyCollection<ChannelLevel>(levels.ToArray());
        Spectrum = spectrum;
        DroppedBufferCount = droppedBufferCount;
        AdvancedMeasurements = advancedMeasurements;
        IsDiscontinuity = isDiscontinuity;
    }

    public DateTimeOffset Timestamp { get; }

    public AudioFormat Format { get; }

    public IReadOnlyList<ChannelLevel> Levels { get; }

    public Spectrum? Spectrum { get; }

    public long DroppedBufferCount { get; }

    public AdvancedMeasurementFrame? AdvancedMeasurements { get; }

    /// <summary>True for the first frame after the source was (re)started: what came before is unrelated audio, so a
    /// history display must not join the two.</summary>
    public bool IsDiscontinuity { get; }
}
