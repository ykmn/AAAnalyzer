using System.Collections.ObjectModel;

namespace SystemAudioAnalyzer.Core;

public sealed class PhaseScopeFrame
{
    public PhaseScopeFrame(IEnumerable<(float Left, float Right)> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = new ReadOnlyCollection<(float Left, float Right)>(points.ToArray());
    }

    public IReadOnlyList<(float Left, float Right)> Points { get; }

    public static PhaseScopeFrame FromInterleaved(ReadOnlySpan<float> samples, int channels, int maximumPoints = 512)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(channels, 2);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumPoints);
        var frameCount = samples.Length / channels;
        var start = Math.Max(0, frameCount - maximumPoints);
        var points = new (float Left, float Right)[frameCount - start];
        for (var frame = start; frame < frameCount; frame++)
        {
            points[frame - start] = (samples[frame * channels], samples[(frame * channels) + 1]);
        }

        return new PhaseScopeFrame(points);
    }
}
