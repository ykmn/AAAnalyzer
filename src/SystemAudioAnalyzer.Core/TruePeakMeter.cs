namespace SystemAudioAnalyzer.Core;

public sealed class TruePeakMeter
{
    private List<float[]> _history = [];
    private float[] _maximum = [];
    private bool[] _overload = [];

    public StereoTruePeakMeasurement Process(ReadOnlySpan<float> interleavedSamples, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);
        EnsureChannels(channels);
        var current = new float[channels];
        var frames = interleavedSamples.Length / channels;
        for (var frame = 0; frame < frames; frame++)
        {
            for (var channel = 0; channel < channels; channel++)
            {
                var value = interleavedSamples[(frame * channels) + channel];
                current[channel] = Math.Max(current[channel], MathF.Abs(value));
                var history = Append(_history[channel], value);
                _history[channel] = history;
                if (history.Length == 4)
                {
                    for (var step = 1; step <= 4; step++)
                    {
                        var interpolated = MathF.Abs(Interpolate(history[0], history[1], history[2], history[3], step / 4f));
                        current[channel] = Math.Max(current[channel], interpolated);
                    }
                }
            }
        }

        for (var channel = 0; channel < channels; channel++)
        {
            _maximum[channel] = Math.Max(_maximum[channel], current[channel]);
            _overload[channel] |= current[channel] >= 1f;
        }

        return new StereoTruePeakMeasurement(current, _maximum, _overload);
    }

    public void Reset(int channel)
    {
        if (_maximum.Length == 0)
        {
            return;
        }

        if (channel < 0 || channel >= _maximum.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }

        _maximum[channel] = 0f;
        _overload[channel] = false;
    }

    public void ResetMaximum(int channel)
    {
        ValidateChannel(channel);
        _maximum[channel] = 0f;
    }

    public void ResetOverload(int channel)
    {
        ValidateChannel(channel);
        _overload[channel] = false;
    }

    private void ValidateChannel(int channel)
    {
        if (_maximum.Length == 0 || channel < 0 || channel >= _maximum.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }
    }

    private void EnsureChannels(int channels)
    {
        if (_maximum.Length == channels)
        {
            return;
        }

        _history = Enumerable.Range(0, channels).Select(_ => Array.Empty<float>()).ToList();
        _maximum = new float[channels];
        _overload = new bool[channels];
    }

    private static float[] Append(float[] history, float value)
    {
        if (history.Length < 4)
        {
            Array.Resize(ref history, history.Length + 1);
            history[^1] = value;
            return history;
        }

        history[0] = history[1];
        history[1] = history[2];
        history[2] = history[3];
        history[3] = value;
        return history;
    }

    private static float Interpolate(float before, float start, float end, float after, float position)
    {
        var positionSquared = position * position;
        var positionCubed = positionSquared * position;
        return 0.5f * ((2f * start)
            + ((-before + end) * position)
            + ((2f * before) - (5f * start) + (4f * end) - after) * positionSquared
            + ((-before + (3f * start) - (3f * end) + after) * positionCubed));
    }
}
