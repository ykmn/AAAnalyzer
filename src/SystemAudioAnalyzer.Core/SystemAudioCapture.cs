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
        (_volumeReader as IDisposable)?.Dispose();
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

    /// <summary>WASAPI loopback capture taps the signal after Windows has already applied the endpoint's master
    /// volume and mute, so the raw buffer IS the post-fader signal; post-fader mode therefore returns it untouched.
    /// Pre-fader mode divides out the current gain to reconstruct the signal as it was before the fader. When muted
    /// or at zero gain, Windows has already discarded the original signal before capture (the loopback buffer is
    /// silence), so there is nothing to recover and the buffer is returned as-is.</summary>
    internal static float[] ApplyFaderGain(float[] samples, FaderMode faderMode, IEndpointVolumeReader volumeReader)
    {
        if (faderMode == FaderMode.PostFader)
        {
            return samples;
        }

        if (volumeReader.IsMuted) return samples;

        var gain = volumeReader.Scalar;
        if (gain <= 0f) return samples;

        var inverseGain = 1f / gain;
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= inverseGain;
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
