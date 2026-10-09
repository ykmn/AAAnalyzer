namespace SystemAudioAnalyzer.App.ViewModels;

public sealed record SourceSelection(SourceMode Mode, OutputDeviceInfo? Device, Uri? StreamUri, FaderMode FaderMode = FaderMode.PreFader);
