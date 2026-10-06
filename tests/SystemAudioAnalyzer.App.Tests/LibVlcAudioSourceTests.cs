using SystemAudioAnalyzer.App.Sources;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class LibVlcAudioSourceTests
{
    [Fact]
    public async Task IcecastUsesOneHundredMillisecondNetworkCache()
    {
        var player = new FakePlayer();
        await using var source = new LibVlcAudioSource(new Uri("https://radio.example/live"), player);

        await source.StartAsync();

        Assert.Equal(100, player.NetworkCachingMilliseconds);
    }

    [Fact]
    public async Task BufferingMapsToBufferingThenRunningStates()
    {
        var player = new FakePlayer();
        await using var source = new LibVlcAudioSource(new Uri("https://radio.example/live.m3u8"), player);
        var states = new List<AudioSourceState>();
        source.StateChanged += (_, eventArgs) => states.Add(eventArgs.State);

        await source.StartAsync();
        player.PublishBuffering(40);
        player.PublishBuffering(100);

        Assert.Contains(AudioSourceState.Buffering, states);
        Assert.Equal(AudioSourceState.Running, source.State);
    }

    [Fact]
    public async Task StopDetachesPcmCallbacksBeforeDisposingThePlayer()
    {
        var player = new FakePlayer();
        await using var source = new LibVlcAudioSource(new Uri("https://radio.example/live"), player);
        var sampleCount = 0;
        source.SamplesAvailable += (_, _) => sampleCount++;
        await source.StartAsync();

        await source.StopAsync();
        player.PublishPcm(new[] { 0.5f, -0.5f }, new AudioFormat(48_000, 2));

        Assert.Equal(0, sampleCount);
        Assert.Equal(1, player.StopCount);
    }

    private sealed class FakePlayer : ILibVlcPlayer
    {
        public event EventHandler<LibVlcPcmEventArgs>? PcmReceived;
        public event EventHandler<float>? BufferingChanged;
        public event EventHandler<Exception>? Failed;
        public int NetworkCachingMilliseconds { get; private set; }
        public int StopCount { get; private set; }

        public Task PlayAsync(Uri streamUri, int networkCachingMilliseconds, CancellationToken cancellationToken = default)
        {
            NetworkCachingMilliseconds = networkCachingMilliseconds;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void PublishPcm(float[] samples, AudioFormat format) =>
            PcmReceived?.Invoke(this, new LibVlcPcmEventArgs(samples, format));

        public void PublishBuffering(float value) => BufferingChanged?.Invoke(this, value);

        public void PublishFailure(Exception exception) => Failed?.Invoke(this, exception);
    }
}
