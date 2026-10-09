using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SystemAudioAnalyzer.Core;

public sealed class SystemAudioCapture : IAudioCapture
{
    private readonly MMDevice _device;
    private readonly MMDeviceEnumerator _enumerator;
    private readonly WasapiLoopbackCapture _capture;
    private readonly FaderMode _faderMode;
    private readonly IEndpointVolumeReader _volumeReader;
    private bool _disposed;

    internal SystemAudioCapture(MMDevice device, MMDeviceEnumerator enumerator, FaderMode faderMode, IEndpointVolumeReader volumeReader)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _faderMode = faderMode;
        _volumeReader = volumeReader ?? throw new ArgumentNullException(nameof(volumeReader));
        _capture = new WasapiLoopbackCapture(_device);
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
    }

    public event EventHandler<AudioSamplesAvailableEventArgs>? SamplesAvailable;

    public event EventHandler<CaptureFaultedEventArgs>? Faulted;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _capture.StartRecording();
    }

    public void Stop()
    {
        if (!_disposed)
        {
            _capture.StopRecording();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _capture.DataAvailable -= OnDataAvailable;
        _capture.RecordingStopped -= OnRecordingStopped;
        _capture.Dispose();
        _device.Dispose();
        _enumerator.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs eventArgs)
    {
        if (eventArgs.BytesRecorded == 0)
        {
            return;
        }

        var samples = PcmSampleConverter.Convert(eventArgs.Buffer.AsSpan(0, eventArgs.BytesRecorded), _capture.WaveFormat);
        samples = ApplyFaderGain(samples, _faderMode, _volumeReader);
        var format = new AudioFormat(_capture.WaveFormat.SampleRate, _capture.WaveFormat.Channels);
        SamplesAvailable?.Invoke(this, new AudioSamplesAvailableEventArgs(samples, format));
    }

    /// <summary>Applies the master-volume gain for post-fader mode in place. Muted audio becomes
    /// silence rather than merely attenuated, matching what a listener actually hears.</summary>
    internal static float[] ApplyFaderGain(float[] samples, FaderMode faderMode, IEndpointVolumeReader volumeReader)
    {
        if (faderMode == FaderMode.PreFader)
        {
            return samples;
        }

        var gain = volumeReader.IsMuted ? 0f : volumeReader.Scalar;
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= gain;
        }

        return samples;
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs eventArgs)
    {
        if (eventArgs.Exception is not null)
        {
            Faulted?.Invoke(this, new CaptureFaultedEventArgs(eventArgs.Exception));
        }
    }
}
