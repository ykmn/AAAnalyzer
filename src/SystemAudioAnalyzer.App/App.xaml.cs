namespace SystemAudioAnalyzer.App;

public partial class App : Application
{
    private readonly Diagnostics.AppLogger _logger = new(AppContext.BaseDirectory);

    public App()
    {
        Startup += OnStartup;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnStartup(object sender, StartupEventArgs eventArgs) =>
        _logger.Write($"AAAnalyzer started. Version={GetType().Assembly.GetName().Version}");

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs eventArgs) =>
        _logger.Write(eventArgs.Exception);

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is Exception exception)
        {
            _logger.Write(exception);
        }
        else
        {
            _logger.Write($"Unhandled non-Exception object: {eventArgs.ExceptionObject}");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs) =>
        _logger.Write(eventArgs.Exception);
}
