using System.ComponentModel;
using System.Globalization;

namespace SystemAudioAnalyzer.App.Localization;

public enum AppLanguage { English, Russian }

/// <summary>
/// The UI language. XAML binds to the indexer through <see cref="TrExtension"/> and refreshes when the language
/// changes; code uses <see cref="T(string)"/>. English is the neutral default until the application picks a language.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    private AppLanguage _language = AppLanguage.English;

    public static Localizer Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (!Enum.IsDefined(value) || value == _language) return;
            _language = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string this[string key] => Get(key);

    public static string T(string key) => Instance.Get(key);

    public static string T(string key, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, T(key), arguments);

    /// <summary>Russian on a Russian system, English otherwise.</summary>
    public static AppLanguage DetectSystemLanguage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? AppLanguage.Russian : AppLanguage.English;

    private string Get(string key)
    {
        if (!Strings.Table.TryGetValue(key, out var pair)) return key;
        return _language == AppLanguage.Russian ? pair.Russian : pair.English;
    }
}
