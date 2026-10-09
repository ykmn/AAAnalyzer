using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.Core;

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
    float? IntegratedLufs,
    bool IsBreak = false)
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

    public void Append(DateTimeOffset timestamp, LoudnessMeasurement measurement, bool isBreak = false)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        _points.Add(new LoudnessHistoryPoint(
            timestamp,
            measurement.MomentaryLufs,
            measurement.ShortTermLufs,
            measurement.IntegratedLufs,
            isBreak));
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

public sealed record MeterDisplayState(float Peak, float Rms, float PeakHold);

/// <summary>Timestamp-driven display ballistics for one meter channel.</summary>
public sealed class MeteringHistory
{
    private bool _initialized;
    private DateTimeOffset _lastTimestamp;
    private DateTimeOffset _peakHoldTimestamp;
    private float _peak;
    private float _rms;
    private float _peakHold;

    public MeterDisplayState Update(ChannelLevel level, DateTimeOffset timestamp, MeterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(settings);

        var peak = Sanitize(level.Peak);
        var rms = Sanitize(level.Rms);
        if (!_initialized)
        {
            _initialized = true;
            _peak = peak;
            _rms = rms;
            _peakHold = peak;
            _lastTimestamp = timestamp;
            _peakHoldTimestamp = timestamp;
            return Current();
        }

        var monotonicTimestamp = timestamp < _lastTimestamp ? _lastTimestamp : timestamp;
        var elapsed = monotonicTimestamp - _lastTimestamp;
        _peak = Smooth(_peak, peak, elapsed, peak >= _peak ? settings.AttackMs : settings.ReleaseMs);
        _rms = Smooth(_rms, rms, elapsed, rms >= _rms ? settings.AttackMs : settings.ReleaseMs);
        _lastTimestamp = monotonicTimestamp;

        if (peak >= _peakHold)
        {
            _peakHold = peak;
            _peakHoldTimestamp = monotonicTimestamp;
        }
        else if ((monotonicTimestamp - _peakHoldTimestamp).TotalMilliseconds >= settings.PeakHoldMs)
        {
            _peakHold = _peak;
            _peakHoldTimestamp = monotonicTimestamp;
        }

        return Current();
    }

    public void Reset()
    {
        _initialized = false;
        _lastTimestamp = default;
        _peakHoldTimestamp = default;
        _peak = 0;
        _rms = 0;
        _peakHold = 0;
    }

    private MeterDisplayState Current() => new(_peak, _rms, _peakHold);

    private static float Sanitize(float value) => float.IsFinite(value) ? Math.Clamp(value, 0f, 16f) : 0f;

    private static float Smooth(float current, float target, TimeSpan elapsed, double durationMs)
    {
        if (durationMs <= 0d)
        {
            return target;
        }

        var alpha = 1d - Math.Exp(-elapsed.TotalMilliseconds / durationMs);
        return (float)(current + ((target - current) * alpha));
    }
}
