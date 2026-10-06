namespace SystemAudioAnalyzer.Core;

public sealed class LevelMeter
{
    public IReadOnlyList<ChannelLevel> Process(ReadOnlySpan<float> samples, int channels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channels);

        var frameCount = samples.Length / channels;
        var peaks = new float[channels];
        var squareSums = new double[channels];

        for (var frame = 0; frame < frameCount; frame++)
        {
            for (var channel = 0; channel < channels; channel++)
            {
                var sample = samples[(frame * channels) + channel];
                peaks[channel] = Math.Max(peaks[channel], MathF.Abs(sample));
                squareSums[channel] += sample * sample;
            }
        }

        var result = new ChannelLevel[channels];
        for (var channel = 0; channel < channels; channel++)
        {
            var rms = frameCount == 0 ? 0 : MathF.Sqrt((float)(squareSums[channel] / frameCount));
            result[channel] = new ChannelLevel(peaks[channel], rms);
        }

        return result;
    }
}
