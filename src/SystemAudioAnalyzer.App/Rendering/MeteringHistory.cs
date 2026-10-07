namespace SystemAudioAnalyzer.App.Rendering;

public sealed record WaterfallRow(DateTimeOffset Timestamp, IReadOnlyList<float> Left, IReadOnlyList<float> Right, int SampleRate, int FftSize);

public sealed class WaterfallHistory
{
    private readonly TimeSpan _visibleDuration;
    private readonly List<WaterfallRow> _rows = [];

    public WaterfallHistory(TimeSpan visibleDuration)
    {
        if (visibleDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleDuration));
        }

        _visibleDuration = visibleDuration;
    }

    public void Append(DateTimeOffset timestamp, IReadOnlyList<float> left, IReadOnlyList<float> right, int sampleRate = 48_000, int fftSize = 4_096)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        _rows.Add(new WaterfallRow(timestamp, left.ToArray(), right.ToArray(), sampleRate, fftSize));
        Trim(timestamp);
    }

    public void Append(AnalysisFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var stereoSpectrum = frame.AdvancedMeasurements?.StereoSpectrum;
        if (stereoSpectrum is null)
        {
            return;
        }

        Append(frame.Timestamp, stereoSpectrum.Left.Magnitudes, stereoSpectrum.Right.Magnitudes, frame.Format.SampleRate, stereoSpectrum.Left.FftSize);
    }

    public IReadOnlyList<WaterfallRow> GetVisibleRows(DateTimeOffset now)
    {
        Trim(now);
        return _rows.ToArray();
    }

    public void Clear() => _rows.Clear();

    private void Trim(DateTimeOffset now)
    {
        var minimumTimestamp = now - _visibleDuration;
        _rows.RemoveAll(row => row.Timestamp < minimumTimestamp);
    }
}

public sealed record LoudnessHistoryPoint(
    DateTimeOffset Timestamp,
    float? MomentaryLufs,
    float? ShortTermLufs,
    float? IntegratedLufs)
{
    public float? Lufs => MomentaryLufs;
}

public sealed class LoudnessHistory
{
    private readonly TimeSpan _visibleDuration;
    private readonly List<LoudnessHistoryPoint> _points = [];

    public LoudnessHistory(TimeSpan visibleDuration)
    {
        if (visibleDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleDuration));
        }

        _visibleDuration = visibleDuration;
    }

    public void Append(DateTimeOffset timestamp, float lufs)
    {
        _points.Add(new LoudnessHistoryPoint(timestamp, lufs, null, null));
        Trim(timestamp);
    }

    public void Append(DateTimeOffset timestamp, LoudnessMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        _points.Add(new LoudnessHistoryPoint(
            timestamp,
            measurement.MomentaryLufs,
            measurement.ShortTermLufs,
            measurement.IntegratedLufs));
        Trim(timestamp);
    }

    public IReadOnlyList<LoudnessHistoryPoint> GetVisiblePoints(DateTimeOffset now)
    {
        Trim(now);
        return _points.ToArray();
    }

    public void Clear() => _points.Clear();

    private void Trim(DateTimeOffset now)
    {
        var minimumTimestamp = now - _visibleDuration;
        _points.RemoveAll(point => point.Timestamp < minimumTimestamp);
    }
}
