using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.ViewModels;

/// <summary>Raised after a toolbar button changed the runtime settings; carries the resulting snapshot.</summary>
public sealed class ToolbarSettingsChangedEventArgs(MeasurementSettings settings) : EventArgs
{
    public MeasurementSettings Settings { get; } = settings;
}
