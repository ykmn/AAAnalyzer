using System.Collections.ObjectModel;

namespace SystemAudioAnalyzer.Core;

public sealed class StereoTruePeakMeasurement
{
    public StereoTruePeakMeasurement(IEnumerable<float> current, IEnumerable<float> maximum, IEnumerable<bool> overload)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(maximum);
        ArgumentNullException.ThrowIfNull(overload);

        Current = new ReadOnlyCollection<float>(current.ToArray());
        Maximum = new ReadOnlyCollection<float>(maximum.ToArray());
        Overload = new ReadOnlyCollection<bool>(overload.ToArray());
        if (Current.Count != Maximum.Count || Current.Count != Overload.Count)
        {
            throw new ArgumentException("True peak channel collections must have equal lengths.");
        }
    }

    public IReadOnlyList<float> Current { get; }

    public IReadOnlyList<float> Maximum { get; }

    public IReadOnlyList<bool> Overload { get; }
}
