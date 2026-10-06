namespace SystemAudioAnalyzer.Core;

public sealed class CaptureFaultedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
