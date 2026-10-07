namespace SystemAudioAnalyzer.App.Settings;

/// <summary>Writes the toolbar-owned values into the default profile, one write at a time and in call order.</summary>
public sealed class ToolbarSettingsPersister(SettingsStore store, Action<Exception> reportError)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task PersistAsync(MeasurementSettings runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var catalog = await store.LoadCatalogAsync().ConfigureAwait(false);
            var profile = catalog.Profiles.Single(candidate => candidate.Id == catalog.DefaultProfileId);
            var updated = ToolbarSettingsActions.CopyToolbarFields(profile.Settings, runtime);
            if (updated != profile.Settings)
            {
                await store.SaveCatalogAsync(catalog.SaveProfile(profile.Id, updated)).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            reportError(exception);
        }
        finally
        {
            _gate.Release();
        }
    }
}
