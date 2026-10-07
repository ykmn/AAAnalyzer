using System.ComponentModel;
using System.Runtime.CompilerServices;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.ViewModels;

public sealed class SettingsDialogViewModel : INotifyPropertyChanged
{
    private readonly SettingsStore _store;
    private readonly SettingsEditSession _session;
    private InstrumentTab _selectedPage;

    public SettingsDialogViewModel(SettingsStore store, MeasurementSettings settings, InstrumentTab selectedPage)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _session = new SettingsEditSession(settings);
        _selectedPage = selectedPage;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MeasurementSettings Current => _session.Current;

    public InstrumentTab SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (_selectedPage != value)
            {
                _selectedPage = value;
                OnPropertyChanged();
            }
        }
    }

    public void Replace(MeasurementSettings settings)
    {
        _session.Replace(settings);
        OnPropertyChanged(nameof(Current));
    }

    public async Task<MeasurementSettings> ApplyAsync(CancellationToken cancellationToken = default)
    {
        var settings = _session.Apply();
        await _store.SaveAsync(settings, cancellationToken);
        return settings;
    }

    public MeasurementSettings Cancel()
    {
        var settings = _session.Cancel();
        OnPropertyChanged(nameof(Current));
        return settings;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
