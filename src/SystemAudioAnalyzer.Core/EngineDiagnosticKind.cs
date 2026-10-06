namespace SystemAudioAnalyzer.Core;

public enum EngineDiagnosticKind
{
    Started,
    Stopped,
    DeviceChanged,
    BufferDropped,
    Faulted,
}
