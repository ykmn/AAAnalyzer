namespace SystemAudioAnalyzer.Core;

public enum AudioSourceState
{
    Stopped,
    Connecting,
    Buffering,
    Running,
    Faulted,
}
