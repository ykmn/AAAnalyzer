namespace SystemAudioAnalyzer.Core;

public interface IAudioCapture : IDisposable
{
    event EventHandler<AudioSamplesAvailableEventArgs>? SamplesAvailable;

    event EventHandler<CaptureFaultedEventArgs>? Faulted;

    void Start();

    void Stop();
}
