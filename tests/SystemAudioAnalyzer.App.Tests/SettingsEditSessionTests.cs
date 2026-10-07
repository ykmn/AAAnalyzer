using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class SettingsEditSessionTests
{
    [Fact]
    public void CancelRestoresTheOpeningSettingsSnapshot()
    {
        var opening = MeasurementSettings.Default;
        var session = new SettingsEditSession(opening);
        session.Replace(opening with { Waterfall = opening.Waterfall with { DisplayFloorDb = -80 } });

        var restored = session.Cancel();

        Assert.Equal(opening, restored);
        Assert.Equal(opening, session.Current);
    }

    [Fact]
    public void ApplyPublishesTheEditedSnapshot()
    {
        var opening = MeasurementSettings.Default;
        var session = new SettingsEditSession(opening);
        var edited = opening with { Phase = opening.Phase with { Gain = 1.5 } };
        session.Replace(edited);

        var applied = session.Apply();

        Assert.Equal(edited, applied);
        Assert.Equal(edited, session.Current);
    }
}
