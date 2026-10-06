namespace SystemAudioAnalyzer.Core;

public sealed class AudioSamplesAvailableEventArgs : EventArgs
{
    public AudioSamplesAvailableEventArgs(float[] samples, AudioFormat format)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(format);

        Samples = samples.ToArray();
        Format = format;
    }

    public float[] Samples { get; }

    public AudioFormat Format { get; }
}
