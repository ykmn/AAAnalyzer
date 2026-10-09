using NAudio.CoreAudioApi;

namespace SystemAudioAnalyzer.Core;

internal sealed class NaudioEndpointVolumeReader : IEndpointVolumeReader
{
    private readonly MMDevice _device;

    public NaudioEndpointVolumeReader(MMDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
    }

    public float Scalar => _device.AudioEndpointVolume.MasterVolumeLevelScalar;

    public bool IsMuted => _device.AudioEndpointVolume.Mute;
}
