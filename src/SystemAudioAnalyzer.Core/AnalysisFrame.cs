using System.Collections.ObjectModel;

namespace SystemAudioAnalyzer.Core;

public sealed class AnalysisFrame
{
    public AnalysisFrame(
        DateTimeOffset timestamp,
        AudioFormat format,
        IEnumerable<ChannelLevel> levels,
        Spectrum? spectrum,
        long droppedBufferCount = 0)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(levels);

        Timestamp = timestamp;
        Format = format;
        Levels = new ReadOnlyCollection<ChannelLevel>(levels.ToArray());
        Spectrum = spectrum;
        DroppedBufferCount = droppedBufferCount;
    }

    public DateTimeOffset Timestamp { get; }

    public AudioFormat Format { get; }

    public IReadOnlyList<ChannelLevel> Levels { get; }

    public Spectrum? Spectrum { get; }

    public long DroppedBufferCount { get; }
}
