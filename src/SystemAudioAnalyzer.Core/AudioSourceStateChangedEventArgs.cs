namespace SystemAudioAnalyzer.Core;

public sealed class AudioSourceStateChangedEventArgs(AudioSourceState state) : EventArgs
{
    public AudioSourceState State { get; } = state;
}
