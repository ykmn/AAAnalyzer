using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace SystemAudioAnalyzer.App.Services;

public sealed class ScreenshotService(string applicationDirectory) : IScreenshotService
{
    private readonly string _applicationDirectory = string.IsNullOrWhiteSpace(applicationDirectory)
        ? throw new ArgumentException("Application directory is required.", nameof(applicationDirectory))
        : applicationDirectory;

    public Task<string> SaveAsync(FrameworkElement element, string paneName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrWhiteSpace(paneName);
        cancellationToken.ThrowIfCancellationRequested();
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            throw new InvalidOperationException("The pane has no visible size.");
        }

        var path = CreateFilePath(_applicationDirectory, paneName, DateTimeOffset.Now);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var dpi = VisualTreeHelper.GetDpi(element);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX),
            (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return Task.FromResult(path);
    }

    public static string CreateFilePath(string applicationDirectory, string paneName, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(paneName);
        var safeName = string.Concat(paneName.Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
        return Path.Combine(applicationDirectory, "Screenshots", $"{safeName}_{timestamp:yyyyMMdd_HHmmss}.png");
    }
}
