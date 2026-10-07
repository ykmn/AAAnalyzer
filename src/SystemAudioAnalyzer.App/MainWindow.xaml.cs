using SystemAudioAnalyzer.App.Services;
using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.App.Diagnostics;
using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.App.Views;

namespace SystemAudioAnalyzer.App;

public partial class MainWindow : Window
{
    private readonly IScreenshotService _screenshotService = new ScreenshotService(AppContext.BaseDirectory);
    private readonly AppLogger _logger = new(AppContext.BaseDirectory);
    private readonly SettingsStore _settingsStore;
    public MainWindow()
    {
        InitializeComponent();
        MeterRail.ResetRequested += ResetMeterRailValue;
        _settingsStore = new SettingsStore(diagnostic: _logger.Write);
        var provider = new NaudioAudioOutputDeviceProvider();
        DataContext = new MainViewModel(new AnalyzerController(), provider.GetActiveDevices());
        Loaded += LoadSettingsAsync;
    }

    private void ResetMeterRailValue(object? sender, Rendering.MeterRailResetEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel viewModel) return;
        if (eventArgs.Target == Rendering.MeterRailResetTarget.Maximum)
            viewModel.ResetTruePeakMaximum(eventArgs.Channel);
        else
            viewModel.ResetTruePeakOverload(eventArgs.Channel);
    }

    private async void LoadSettingsAsync(object sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.MeasurementSettings = await _settingsStore.LoadAsync();
        }
    }

    private void SetRtaResolution(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is FrameworkElement { Tag: string name } && DataContext is MainViewModel viewModel && Enum.TryParse<RtaResolution>(name, out var resolution))
        {
            viewModel.RtaResolution = resolution;
        }
    }

    private void SetRtaChannel(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is FrameworkElement { Tag: string name } && DataContext is MainViewModel viewModel && Enum.TryParse<RtaChannelMode>(name, out var mode))
        {
            viewModel.RtaChannelMode = mode;
        }
    }

    private void ResetPane(object sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.ResetAllMeasurements();
        }

        WaterfallView.Reset();
        LoudnessView.Reset();
    }

    private async void SaveScreenshot(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not FrameworkElement { Tag: string paneName } || FindName(paneName) is not FrameworkElement pane)
        {
            return;
        }

        try
        {
            await _screenshotService.SaveAsync(pane, paneName);
        }
        catch (Exception exception)
        {
            _logger.Write(exception);
        }
    }

    private void OpenSettings(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not FrameworkElement { Tag: string name } || !Enum.TryParse<InstrumentTab>(name, out var page))
        {
            return;
        }

        if (DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        var dialog = new SettingsWindow(new SettingsDialogViewModel(_settingsStore, mainViewModel.MeasurementSettings, page)) { Owner = this };
        dialog.SettingsSaveFailed += (_, args) => _logger.Write(args.Exception);
        dialog.SettingsApplied += (_, args) => mainViewModel.MeasurementSettings = args.Settings;
        dialog.SettingsCancelled += (_, args) => mainViewModel.MeasurementSettings = args.Settings;
        dialog.ShowDialog();
    }
}
