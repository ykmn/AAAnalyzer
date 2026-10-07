using SystemAudioAnalyzer.App.Services;
using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.App.Diagnostics;

namespace SystemAudioAnalyzer.App;

public partial class MainWindow : Window
{
    private readonly IScreenshotService _screenshotService = new ScreenshotService(AppContext.BaseDirectory);
    private readonly AppLogger _logger = new(AppContext.BaseDirectory);
    public MainWindow()
    {
        InitializeComponent();
        var provider = new NaudioAudioOutputDeviceProvider();
        DataContext = new MainViewModel(new AnalyzerController(), provider.GetActiveDevices());
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
}
