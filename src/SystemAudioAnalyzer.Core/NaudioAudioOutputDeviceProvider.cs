using NAudio.CoreAudioApi;
using System.Runtime.InteropServices;

namespace SystemAudioAnalyzer.Core;

public sealed class NaudioAudioOutputDeviceProvider : IAudioOutputDeviceProvider
{
    public IReadOnlyList<OutputDeviceInfo> GetActiveDevices()
    {
        using var enumerator = new MMDeviceEnumerator();
        var defaultId = TryGetDefaultDeviceId(enumerator);
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        var result = new List<OutputDeviceInfo>(devices.Count);

        foreach (var device in devices)
        {
            using (device)
            {
                result.Add(new OutputDeviceInfo(device.ID, device.FriendlyName, device.ID == defaultId));
            }
        }

        return result;
    }

    public OutputDeviceInfo? GetDefaultDevice()
    {
        using var enumerator = new MMDeviceEnumerator();
        try
        {
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return new OutputDeviceInfo(device.ID, device.FriendlyName, IsDefault: true);
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string? TryGetDefaultDeviceId(MMDeviceEnumerator enumerator)
    {
        try
        {
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            return device.ID;
        }
        catch (COMException)
        {
            return null;
        }
    }
}
