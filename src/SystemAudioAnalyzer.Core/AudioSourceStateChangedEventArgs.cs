namespace SystemAudioAnalyzer.Core;

public sealed class AudioSourceStateChangedEventArgs(AudioSourceState state, float? bufferingPercent = null) : EventArgs
{
    public AudioSourceState State { get; } = state;

    /// <summary>Fill of the stream buffer (0-100) while <see cref="AudioSourceState.Buffering"/>; otherwise null.</summary>
    public float? BufferingPercent { get; } = bufferingPercent;
}
