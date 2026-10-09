using NAudio.CoreAudioApi;

namespace SystemAudioAnalyzer.Core;

public sealed class NaudioAudioCaptureFactory : IAudioCaptureFactory
{
    public IAudioCapture Create(OutputDeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var enumerator = new MMDeviceEnumerator();
        try
        {
            var endpoint = enumerator.GetDevice(device.Id);
            var volumeReader = new DefaultVolumeReader();
            return new SystemAudioCapture(endpoint, enumerator, FaderMode.PreFader, volumeReader);
        }
        catch
        {
            enumerator.Dispose();
            throw;
        }
    }

    private sealed class DefaultVolumeReader : IEndpointVolumeReader
    {
        public float Scalar => 1f;
        public bool IsMuted => false;
    }
}
