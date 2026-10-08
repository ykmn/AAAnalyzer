using System.ComponentModel;
using SystemAudioAnalyzer.App.Localization;
using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsDialogViewModel _viewModel;
    private readonly Func<string, string?> _colorPicker;
    private bool _completed;

    public event EventHandler<SettingsChangedEventArgs>? SettingsApplied;
    public event EventHandler<SettingsChangedEventArgs>? SettingsCancelled;
    public event EventHandler<SettingsSaveFailedEventArgs>? SettingsSaveFailed;

    public SettingsWindow(SettingsDialogViewModel viewModel, Func<string, string?>? colorPicker = null)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _colorPicker = colorPicker ?? ChooseSystemColor;
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
        viewModel.SelectedPage = Pages.SelectedIndex switch
        {
            0 => InstrumentTab.Analyzer,
            1 => InstrumentTab.Waterfall,
            2 => InstrumentTab.Meters,
            3 => InstrumentTab.Loudness,
            4 => InstrumentTab.Rta,
            _ => InstrumentTab.Phase,
        };
        if (Pages.SelectedIndex == 3) viewModel.SelectedGradientKind = GradientKind.Loudness;
        else if (Pages.SelectedIndex == 1) viewModel.SelectedGradientKind = GradientKind.Waterfall;
        LanguageBox.ItemsSource = new SettingsOption<AppLanguage>[] { new("English", AppLanguage.English), new("Русский", AppLanguage.Russian) };
        LanguageBox.SelectedValue = Localizer.Instance.Language;
        Closing += RestoreRuntimeOnWindowClose;
    }

    // The language applies at once and is stored on its own, not in the profile or the Apply/Cancel draft.
    private void LanguageChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs eventArgs)
    {
        if (LanguageBox.SelectedValue is AppLanguage language) Localizer.Instance.Language = language;
    }

    private void Accept(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            var settings = _viewModel.Apply();
            SettingsApplied?.Invoke(this, new SettingsChangedEventArgs(settings));
            _completed = true;
            DialogResult = true;
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void Apply(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            var settings = _viewModel.Apply();
            SettingsApplied?.Invoke(this, new SettingsChangedEventArgs(settings));
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void Cancel(object sender, RoutedEventArgs eventArgs)
    {
        RestoreOpeningRuntime();
        _completed = true;
        DialogResult = false;
    }

    private async void SaveProfile(object sender, RoutedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.ProfileName))
            await RunStoreAction(() => _viewModel.SaveAsync());
        else
            await RunStoreAction(() => _viewModel.SaveAsAsync(_viewModel.ProfileName));
    }

    private async void SetDefault(object sender, RoutedEventArgs eventArgs) =>
        await RunStoreAction(() => _viewModel.SetDefaultAsync());

    private async void DeleteProfile(object sender, RoutedEventArgs eventArgs) =>
        await RunStoreAction(() => _viewModel.DeleteAsync());

    private async void SaveRuntimeAsDefault(object sender, RoutedEventArgs eventArgs) =>
        await RunStoreAction(() => _viewModel.SaveCurrentRuntimeAsDefaultAsync());

    private void AddGradientStop(object sender, RoutedEventArgs eventArgs) => _viewModel.AddGradientStop();

    private void PageSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs eventArgs)
    {
        if (DataContext is not SettingsDialogViewModel viewModel) return;
        viewModel.SelectedPage = Pages.SelectedIndex switch
        {
            0 => InstrumentTab.Analyzer,
            1 => InstrumentTab.Waterfall,
            2 => InstrumentTab.Meters,
            3 => InstrumentTab.Loudness,
            4 => InstrumentTab.Rta,
            _ => InstrumentTab.Phase,
        };
        if (Pages.SelectedIndex == 3) viewModel.SelectedGradientKind = GradientKind.Loudness;
        else if (Pages.SelectedIndex == 1) viewModel.SelectedGradientKind = GradientKind.Waterfall;
    }

    private void DeleteGradientStop(object sender, RoutedEventArgs eventArgs) => _viewModel.DeleteGradientStop();

    private void PickGradientColor(object sender, RoutedEventArgs eventArgs)
    {
        var selectedColor = _colorPicker(_viewModel.SelectedGradientColor);
        if (selectedColor is not null) _viewModel.UpdateSelectedGradientColor(selectedColor);
    }

    private void PickColor(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not FrameworkElement { Tag: string propertyName }) return;
        var current = ReadColor(propertyName);
        var selected = _colorPicker(current);
        if (selected is null) return;
        switch (propertyName)
        {
            case nameof(SettingsDialogViewModel.AnalyzerCursorColor): _viewModel.AnalyzerCursorColor = selected; break;
            case nameof(SettingsDialogViewModel.AnalyzerTextColor): _viewModel.AnalyzerTextColor = selected; break;
            case nameof(SettingsDialogViewModel.WaterfallPaletteColor): _viewModel.WaterfallPaletteColor = selected; break;
            case nameof(SettingsDialogViewModel.PeakColor): _viewModel.PeakColor = selected; break;
            case nameof(SettingsDialogViewModel.RmsColor): _viewModel.RmsColor = selected; break;
            case nameof(SettingsDialogViewModel.LufsColor): _viewModel.LufsColor = selected; break;
            case nameof(SettingsDialogViewModel.ClipColor): _viewModel.ClipColor = selected; break;
            case nameof(SettingsDialogViewModel.MeterColor): _viewModel.MeterColor = selected; break;
            case nameof(SettingsDialogViewModel.OverloadColor): _viewModel.OverloadColor = selected; break;
            case nameof(SettingsDialogViewModel.RtaBarColor): _viewModel.RtaBarColor = selected; break;
            case nameof(SettingsDialogViewModel.RtaPeakCapColor): _viewModel.RtaPeakCapColor = selected; break;
            case nameof(SettingsDialogViewModel.RtaTargetBandColor): _viewModel.RtaTargetBandColor = selected; break;
        }
    }

    private string ReadColor(string propertyName) => propertyName switch
    {
        nameof(SettingsDialogViewModel.AnalyzerCursorColor) => _viewModel.AnalyzerCursorColor,
        nameof(SettingsDialogViewModel.AnalyzerTextColor) => _viewModel.AnalyzerTextColor,
        nameof(SettingsDialogViewModel.WaterfallPaletteColor) => _viewModel.WaterfallPaletteColor,
        nameof(SettingsDialogViewModel.PeakColor) => _viewModel.PeakColor,
        nameof(SettingsDialogViewModel.RmsColor) => _viewModel.RmsColor,
        nameof(SettingsDialogViewModel.LufsColor) => _viewModel.LufsColor,
        nameof(SettingsDialogViewModel.ClipColor) => _viewModel.ClipColor,
        nameof(SettingsDialogViewModel.MeterColor) => _viewModel.MeterColor,
        nameof(SettingsDialogViewModel.OverloadColor) => _viewModel.OverloadColor,
        nameof(SettingsDialogViewModel.RtaBarColor) => _viewModel.RtaBarColor,
        nameof(SettingsDialogViewModel.RtaPeakCapColor) => _viewModel.RtaPeakCapColor,
        nameof(SettingsDialogViewModel.RtaTargetBandColor) => _viewModel.RtaTargetBandColor,
        _ => "#FFFFFF",
    };

    private static string? ChooseSystemColor(string initialColor)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
        try { dialog.Color = System.Drawing.ColorTranslator.FromHtml(initialColor); }
        catch (FormatException) { dialog.Color = System.Drawing.Color.White; }
        return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK
            ? System.Drawing.ColorTranslator.ToHtml(dialog.Color)
            : null;
    }

    private async Task RunStoreAction(Func<Task> action)
    {
        try
        {
            await action();
            SaveError.Text = string.Empty;
        }
        catch (Exception exception) { ShowError(exception); }
    }

    private void ShowError(Exception exception)
    {
        SettingsSaveFailed?.Invoke(this, new SettingsSaveFailedEventArgs(exception));
        SaveError.Text = exception is InvalidOperationException or ArgumentException
            ? string.IsNullOrWhiteSpace(_viewModel.ValidationMessage)
                ? exception.Message
                : _viewModel.ValidationMessage
            : Localizer.T("SaveProfileFailed");
    }

    private void RestoreRuntimeOnWindowClose(object? sender, CancelEventArgs eventArgs)
    {
        if (!_completed) RestoreOpeningRuntime();
    }

    private void RestoreOpeningRuntime()
    {
        var settings = _viewModel.Cancel();
        SettingsCancelled?.Invoke(this, new SettingsChangedEventArgs(settings));
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
