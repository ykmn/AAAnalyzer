namespace SystemAudioAnalyzer.Core;

public sealed record EngineDiagnostic(
    DateTimeOffset Timestamp,
    EngineDiagnosticKind Kind,
    string Message,
    OutputDeviceInfo? Device,
    AudioFormat? Format,
    long DroppedBufferCount,
    Exception? Exception);
