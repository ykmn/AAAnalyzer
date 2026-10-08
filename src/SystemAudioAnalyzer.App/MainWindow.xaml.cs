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
    private readonly StreamHistoryStore _streamHistoryStore;
    public MainWindow()
    {
        Sources.LibVlcRuntime.WarmUp(exception => _logger.Write(exception));
        var preferences = new Localization.AppPreferences(Path.Combine(AppContext.BaseDirectory, "Data"), _logger.Write);
        Localization.Localizer.Instance.Language = preferences.LoadLanguage();
        Localization.Localizer.Instance.LanguageChanged += (_, _) => preferences.SaveLanguage(Localization.Localizer.Instance.Language);
        InitializeComponent();
        MeterRail.ResetRequested += ResetMeterRailValue;
        LoudnessView.RangeChanged += (_, range) => Dispatcher.BeginInvoke(() => MeterRail.LoudnessRange = range);
        _settingsStore = new SettingsStore(diagnostic: _logger.Write);
        _streamHistoryStore = new StreamHistoryStore(Path.Combine(AppContext.BaseDirectory, "Data"), _logger.Write);
        var provider = new NaudioAudioOutputDeviceProvider();
        _toolbarPersister = new ToolbarSettingsPersister(_settingsStore, _logger.Write);
        var mainViewModel = new MainViewModel(new AnalyzerController(_logger.Write), provider.GetActiveDevices());
        mainViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.StatusText)) _logger.Write($"Status: {mainViewModel.StatusText}");
        };
        mainViewModel.ToolbarSettingsChanged += async (_, args) => await _toolbarPersister.PersistAsync(args.Settings);
        mainViewModel.StreamHistoryChanged += async (_, _) => await _streamHistoryStore.SaveAsync(mainViewModel.StreamHistory.ToArray());
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
            viewModel.LoadStreamHistory(await _streamHistoryStore.LoadAsync());
        }
    }

    private void LoudnessZoomOut(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.ZoomLoudness(1.5);

    private void LoudnessZoomIn(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.ZoomLoudness(1 / 1.5);

    private void LoudnessShiftDown(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.ShiftLoudness(-1);

    private void LoudnessShiftUp(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.ShiftLoudness(1);

    private void LoudnessTargetDown(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.AdjustLoudnessTarget(-1);

    private void LoudnessTargetUp(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.AdjustLoudnessTarget(1);

    private void ClearStreamHistory(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.ClearStreamHistory();

    private void RtaAverageDown(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.AdjustRtaAveraging(-10);

    private void RtaAverageUp(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.AdjustRtaAveraging(10);

    private void RtaTiltToggle(object sender, RoutedEventArgs eventArgs) => (DataContext as MainViewModel)?.ToggleRtaTilt();

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
        RtaView.Reset();
        MeterRail.ResetPhase();
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
                () => MessageBox.Show(this, Localization.Localizer.T("ConfirmDiscardText"), Localization.Localizer.T("ConfirmDiscardTitle"),
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
