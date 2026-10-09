using NAudio.CoreAudioApi;

namespace SystemAudioAnalyzer.Core;

/// <summary>Caches the endpoint's master volume gain and mute state via <see cref="AudioEndpointVolume.OnVolumeNotification"/>
/// instead of querying the COM object directly. The COM object is activated on the thread that constructs this reader (the
/// UI thread); reading its properties from the WASAPI capture thread throws, so the capture thread only ever reads plain
/// cached fields kept current by the notification callback.</summary>
internal sealed class NaudioEndpointVolumeReader : IEndpointVolumeReader, IDisposable
{
    private readonly AudioEndpointVolume _volume;
    private volatile float _scalar;
    private volatile bool _isMuted;

    public NaudioEndpointVolumeReader(MMDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _volume = device.AudioEndpointVolume;
        _scalar = _volume.MasterVolumeLevelScalar;
        _isMuted = _volume.Mute;
        _volume.OnVolumeNotification += OnVolumeNotification;
    }

    public float Scalar => _scalar;

    public bool IsMuted => _isMuted;

    private void OnVolumeNotification(AudioVolumeNotificationData data)
    {
        _scalar = data.MasterVolume;
        _isMuted = data.Muted;
    }

    public void Dispose() => _volume.OnVolumeNotification -= OnVolumeNotification;
}
