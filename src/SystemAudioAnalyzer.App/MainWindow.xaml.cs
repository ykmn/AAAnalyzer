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
    private readonly ToolbarSettingsPersister _toolbarPersister;
    public MainWindow()
    {
        InitializeComponent();
        MeterRail.ResetRequested += ResetMeterRailValue;
        _settingsStore = new SettingsStore(diagnostic: _logger.Write);
        var provider = new NaudioAudioOutputDeviceProvider();
        _toolbarPersister = new ToolbarSettingsPersister(_settingsStore, _logger.Write);
        var mainViewModel = new MainViewModel(new AnalyzerController(), provider.GetActiveDevices());
        mainViewModel.ToolbarSettingsChanged += async (_, args) => await _toolbarPersister.PersistAsync(args.Settings);
        DataContext = mainViewModel;
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
            viewModel.MeasurementSettings = await _settingsStore.LoadStartupSettingsAsync();
        }
    }

    private void CycleRolling(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.CycleRollingWindow();

    private void LoudnessZoomOut(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.ZoomLoudness(1.5);

    private void LoudnessZoomIn(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.ZoomLoudness(1 / 1.5);

    private void LoudnessShiftDown(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.ShiftLoudness(-1);

    private void LoudnessShiftUp(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.ShiftLoudness(1);

    private void RtaAverageDown(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.AdjustRtaAveraging(-10);

    private void RtaAverageUp(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.AdjustRtaAveraging(10);

    private void RtaTargetDown(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.AdjustRtaTarget(-1);

    private void RtaTargetUp(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.AdjustRtaTarget(1);

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
        if (DataContext is not MainViewModel activeViewModel)
        {
            return;
        }

        var paneName = $"{activeViewModel.ActiveTab}Pane";
        if (FindName(paneName) is not FrameworkElement pane)
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

    private async void OpenSettings(object sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel mainViewModel)
        {
            return;
        }

        var page = mainViewModel.ActiveTab;

        try
        {
            var catalog = await _settingsStore.LoadCatalogAsync();
            var viewModel = new SettingsDialogViewModel(_settingsStore, catalog, mainViewModel.MeasurementSettings, page,
                () => MessageBox.Show(this, "Discard the unsaved changes and switch profile?", "Unsaved changes",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
            var dialog = new SettingsWindow(viewModel) { Owner = this };
            dialog.SettingsSaveFailed += (_, args) => _logger.Write(args.Exception);
            dialog.SettingsApplied += (_, args) => mainViewModel.MeasurementSettings = args.Settings;
            dialog.SettingsCancelled += (_, args) => mainViewModel.MeasurementSettings = args.Settings;
            dialog.ShowDialog();
        }
        catch (Exception exception) { _logger.Write(exception); }
    }
}
