using System.Text;
using System.IO;

namespace SystemAudioAnalyzer.App.Diagnostics;

public sealed class AppLogger
{
    private readonly object _gate = new();
    private readonly string _logFilePath;

    public AppLogger(string applicationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationDirectory);
        _logFilePath = Path.Combine(applicationDirectory, "logs", "AAAnalyzer.log");
    }

    public void Write(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        WriteLine(message);
    }

    public void Write(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        WriteLine(exception.ToString());
    }

    private void WriteLine(string value)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath)!);
            File.AppendAllText(
                _logFilePath,
                $"{DateTimeOffset.Now:O} {value}{Environment.NewLine}",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
