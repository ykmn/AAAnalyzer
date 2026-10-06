namespace SystemAudioAnalyzer.App.Sources;

public interface ILibVlcPlayer : IAsyncDisposable
{
    event EventHandler<LibVlcPcmEventArgs>? PcmReceived;

    event EventHandler<float>? BufferingChanged;

    event EventHandler<Exception>? Failed;

    Task PlayAsync(Uri streamUri, int networkCachingMilliseconds, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed class LibVlcPcmEventArgs(float[] samples, AudioFormat format) : EventArgs
{
    public float[] Samples { get; } = samples ?? throw new ArgumentNullException(nameof(samples));

    public AudioFormat Format { get; } = format ?? throw new ArgumentNullException(nameof(format));
}
