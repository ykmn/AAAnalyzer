using System.IO;
using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Views;

public partial class UrlLibraryWindow : Window
{
    private readonly UrlLibraryViewModel _viewModel;
    private readonly Action<string> _diagnostic;

    public UrlLibraryWindow(UrlLibraryViewModel viewModel, Action<string>? diagnostic = null)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _diagnostic = diagnostic ?? (_ => { });
        DataContext = viewModel;
        // Keep the window instance alive across close/reopen so the main window can Show() it again.
        Closing += (_, closingArgs) => { closingArgs.Cancel = true; Hide(); };
    }

    private void Add(object sender, RoutedEventArgs eventArgs) => _viewModel.TryAdd();

    private void SaveEdit(object sender, RoutedEventArgs eventArgs) => _viewModel.TrySaveEdit();

    private void Remove(object sender, RoutedEventArgs eventArgs) => _viewModel.Remove();

    private void MoveUp(object sender, RoutedEventArgs eventArgs) => _viewModel.MoveUp();

    private void MoveDown(object sender, RoutedEventArgs eventArgs) => _viewModel.MoveDown();

    private void ActivateSelected(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        if (_viewModel.SelectedEntry is { } entry) _viewModel.Activate(entry);
    }

    private async void ImportPlaylist(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = Localization.Localizer.T("UrlLibraryPlaylistFilter") + "|*.m3u;*.m3u8",
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var imported = await M3uPlaylistParser.ParseFileAsync(dialog.FileName);
            _viewModel.ImportPlaylist(imported);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _diagnostic($"Playlist import failed: {exception.Message}");
        }
    }
}
