namespace SystemAudioAnalyzer.Core;

public sealed class EngineFaultedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
