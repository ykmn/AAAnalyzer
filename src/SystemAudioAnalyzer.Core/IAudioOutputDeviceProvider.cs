namespace SystemAudioAnalyzer.Core;

public interface IAudioOutputDeviceProvider
{
    IReadOnlyList<OutputDeviceInfo> GetActiveDevices();

    OutputDeviceInfo? GetDefaultDevice();
}
