using NAudio.CoreAudioApi;

namespace SystemAudioAnalyzer.Core;

public sealed class NaudioAudioCaptureFactory : IAudioCaptureFactory
{
    public IAudioCapture Create(OutputDeviceInfo device, FaderMode faderMode)
    {
        ArgumentNullException.ThrowIfNull(device);

        var enumerator = new MMDeviceEnumerator();
        try
        {
            var endpoint = enumerator.GetDevice(device.Id);
            return new SystemAudioCapture(endpoint, enumerator, faderMode, new NaudioEndpointVolumeReader(endpoint));
        }
        catch
        {
            enumerator.Dispose();
            throw;
        }
    }
}
