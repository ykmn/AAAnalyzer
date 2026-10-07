using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsDialogViewModel _viewModel;

    public event EventHandler<SettingsChangedEventArgs>? SettingsApplied;

    public event EventHandler<SettingsChangedEventArgs>? SettingsCancelled;

    public event EventHandler<SettingsSaveFailedEventArgs>? SettingsSaveFailed;

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
        try
        {
            var settings = await _viewModel.ApplyAsync();
            SettingsApplied?.Invoke(this, new SettingsChangedEventArgs(settings));
            DialogResult = true;
        }
        catch (Exception exception) { ShowSaveError(exception); }
    }

    private async void Apply(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            var settings = await _viewModel.ApplyAsync();
            SettingsApplied?.Invoke(this, new SettingsChangedEventArgs(settings));
        }
        catch (Exception exception) { ShowSaveError(exception); }
    }

    private async void Cancel(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            var settings = await _viewModel.CancelAsync();
            SettingsCancelled?.Invoke(this, new SettingsChangedEventArgs(settings));
            DialogResult = false;
        }
        catch (Exception exception) { ShowSaveError(exception); }
    }

    private void ShowSaveError(Exception exception)
    {
        SettingsSaveFailed?.Invoke(this, new SettingsSaveFailedEventArgs(exception));
        SaveError.Text = exception is ArgumentException
            ? "Проверьте значения параметров и формат цветов."
            : "Не удалось сохранить настройки. Проверьте доступ к профилю пользователя.";
    }
}

public sealed class SettingsChangedEventArgs(MeasurementSettings settings) : EventArgs
{
    public MeasurementSettings Settings { get; } = settings;
}

public sealed class SettingsSaveFailedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
