using System.Globalization;
using System.Windows.Data;

namespace SystemAudioAnalyzer.App.ViewModels;

/// <summary>Maps a value to "equals the converter parameter" so radio-style buttons can two-way bind an enum or number.</summary>
public sealed class ValueMatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && string.Equals(value.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null) return System.Windows.Data.Binding.DoNothing;
        var text = parameter.ToString()!;
        return targetType.IsEnum ? Enum.Parse(targetType, text) : System.Convert.ChangeType(text, targetType, CultureInfo.InvariantCulture);
    }
}
