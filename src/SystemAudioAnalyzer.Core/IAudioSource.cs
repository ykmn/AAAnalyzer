namespace SystemAudioAnalyzer.Core;

/// <summary>Produces decoded PCM samples for analysis.</summary>
public interface IAudioSource : IAsyncDisposable
{
    event EventHandler<AudioSamplesAvailableEventArgs>? SamplesAvailable;

    event EventHandler<AudioSourceStateChangedEventArgs>? StateChanged;

    event EventHandler<CaptureFaultedEventArgs>? Faulted;

    AudioSourceState State { get; }

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
