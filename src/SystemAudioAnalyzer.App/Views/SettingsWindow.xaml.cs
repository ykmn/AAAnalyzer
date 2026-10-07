using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsDialogViewModel _viewModel;

    public event EventHandler<SettingsChangedEventArgs>? SettingsApplied;

    public event EventHandler<SettingsChangedEventArgs>? SettingsCancelled;

    public SettingsWindow(SettingsDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Pages.SelectedIndex = viewModel.SelectedPage switch
        {
            InstrumentTab.Analyzer => 0,
            InstrumentTab.Waterfall => 1,
            InstrumentTab.Meters => 2,
            InstrumentTab.Loudness => 3,
            InstrumentTab.Rta => 4,
            _ => 5,
        };
    }

    private async void Accept(object sender, RoutedEventArgs eventArgs)
    {
        var settings = await _viewModel.ApplyAsync();
        SettingsApplied?.Invoke(this, new SettingsChangedEventArgs(settings));
        DialogResult = true;
    }

    private async void Apply(object sender, RoutedEventArgs eventArgs)
    {
        var settings = await _viewModel.ApplyAsync();
        SettingsApplied?.Invoke(this, new SettingsChangedEventArgs(settings));
    }

    private async void Cancel(object sender, RoutedEventArgs eventArgs)
    {
        var settings = await _viewModel.CancelAsync();
        SettingsCancelled?.Invoke(this, new SettingsChangedEventArgs(settings));
        DialogResult = false;
    }
}

public sealed class SettingsChangedEventArgs(MeasurementSettings settings) : EventArgs
{
    public MeasurementSettings Settings { get; } = settings;
}
