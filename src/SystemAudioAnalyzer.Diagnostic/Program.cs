// See https://aka.ms/new-console-template for more information
using SystemAudioAnalyzer.Core;

var provider = new NaudioAudioOutputDeviceProvider();
var devices = provider.GetActiveDevices();

if (devices.Count == 0)
{
    Console.Error.WriteLine("No active audio output devices were found.");
    return 1;
}

Console.WriteLine("Active output devices:");
foreach (var device in devices)
{
    Console.WriteLine($"- {device.Name} [{device.Id}]{(device.IsDefault ? " (default)" : string.Empty)}");
}

if (args.Contains("--list", StringComparer.OrdinalIgnoreCase))
{
    return 0;
}

var selectedDevice = provider.GetDefaultDevice();
if (selectedDevice is null)
{
    Console.Error.WriteLine("The default audio output device is unavailable.");
    return 1;
}

Console.WriteLine();
Console.WriteLine($"Analyzing: {selectedDevice.Name}");
Console.WriteLine("Start audio playback, then press Ctrl+C to stop.");

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

await using var engine = new AudioAnalysisEngine(provider, new NaudioAudioCaptureFactory());
await engine.StartAsync(selectedDevice);

try
{
    await foreach (var frame in engine.ReadFrames(cancellation.Token))
    {
        var levels = string.Join(
            ", ",
            frame.Levels.Select((level, index) => $"ch{index + 1}: peak {ToDb(level.Peak):F1} dB, rms {ToDb(level.Rms):F1} dB"));
        var spectrum = frame.Spectrum is null
            ? "spectrum: collecting window"
            : $"peak frequency: {GetPeakFrequency(frame.Spectrum):F0} Hz";

        Console.WriteLine($"{frame.Timestamp:HH:mm:ss.fff} | {levels} | {spectrum}");
    }
}
catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
{
}
finally
{
    await engine.StopAsync();
}

return 0;

static float ToDb(float value) => value <= 0 ? -96f : 20f * MathF.Log10(value);

static float GetPeakFrequency(Spectrum spectrum)
{
    var peak = spectrum.Magnitudes
        .Select((magnitude, index) => (magnitude, index))
        .MaxBy(item => item.magnitude);
    return spectrum.GetFrequencyHz(peak.index);
}
