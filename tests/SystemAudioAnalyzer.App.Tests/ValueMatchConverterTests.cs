using System.Globalization;
using SystemAudioAnalyzer.App.ViewModels;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class ValueMatchConverterTests
{
    private readonly ValueMatchConverter _converter = new();

    [Fact]
    public void ConvertComparesTheValueWithTheParameterText()
    {
        Assert.Equal(true, _converter.Convert(RtaResolution.OneThird, typeof(bool), "OneThird", CultureInfo.InvariantCulture));
        Assert.Equal(false, _converter.Convert(RtaResolution.One, typeof(bool), "OneThird", CultureInfo.InvariantCulture));
        Assert.Equal(true, _converter.Convert(600, typeof(bool), "600", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBackParsesEnumsAndNumbersOnlyWhenChecked()
    {
        Assert.Equal(RtaResolution.OneSixth, _converter.ConvertBack(true, typeof(RtaResolution), "OneSixth", CultureInfo.InvariantCulture));
        Assert.Equal(300, _converter.ConvertBack(true, typeof(int), "300", CultureInfo.InvariantCulture));
        Assert.Same(System.Windows.Data.Binding.DoNothing, _converter.ConvertBack(false, typeof(int), "300", CultureInfo.InvariantCulture));
    }
}
