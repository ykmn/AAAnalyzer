namespace SystemAudioAnalyzer.Core;

/// <summary>Adapts the existing synchronous capture API to an analysis source.</summary>
public sealed class AudioCaptureSource : IAudioSource
{
    private readonly IAudioCapture _capture;
    private bool _disposed;

    public AudioCaptureSource(IAudioCapture capture)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _capture.SamplesAvailable += ForwardSamples;
        _capture.Faulted += ForwardFault;
    }

    public event EventHandler<AudioSamplesAvailableEventArgs>? SamplesAvailable;

    public event EventHandler<CaptureFaultedEventArgs>? Faulted;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
        _capture.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_disposed)
        {
            _capture.Stop();
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _capture.SamplesAvailable -= ForwardSamples;
        _capture.Faulted -= ForwardFault;
        _capture.Dispose();
        return ValueTask.CompletedTask;
    }

    private void ForwardSamples(object? sender, AudioSamplesAvailableEventArgs eventArgs) =>
        SamplesAvailable?.Invoke(this, eventArgs);

    private void ForwardFault(object? sender, CaptureFaultedEventArgs eventArgs) =>
        Faulted?.Invoke(this, eventArgs);

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioCaptureSource));
        }
    }
}
