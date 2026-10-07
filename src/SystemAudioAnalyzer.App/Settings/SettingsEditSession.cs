namespace SystemAudioAnalyzer.App.Settings;

public sealed class SettingsEditSession
{
    private readonly MeasurementSettings _opening;

    public SettingsEditSession(MeasurementSettings opening)
    {
        _opening = opening ?? throw new ArgumentNullException(nameof(opening));
        Current = opening;
    }

    public MeasurementSettings Current { get; private set; }

    public void Replace(MeasurementSettings settings) => Current = settings ?? throw new ArgumentNullException(nameof(settings));

    public MeasurementSettings Apply() => Current;

    public MeasurementSettings Cancel()
    {
        Current = _opening;
        return Current;
    }
}
