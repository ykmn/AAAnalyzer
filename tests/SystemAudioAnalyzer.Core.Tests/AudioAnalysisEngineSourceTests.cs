namespace SystemAudioAnalyzer.Core.Tests;

public sealed class AudioAnalysisEngineSourceTests
{
    [Fact]
    public async Task StartWithExternalSourcePublishesFramesAndStopsTheSource()
    {
        var device = new OutputDeviceInfo("default", "Speakers", IsDefault: true);
        var source = new FakeAudioSource();
        await using var engine = new AudioAnalysisEngine(new FakeDeviceProvider(device), new FakeCaptureFactory());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await engine.StartAsync(source);
        source.Publish(new[] { 0.5f, -0.25f, -0.5f, 0.25f }, new AudioFormat(48_000, channels: 2));

        await using var frames = engine.ReadFrames(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(0.5f, frames.Current.Levels[0].Peak);

        await engine.StopAsync();

        Assert.Equal(1, source.StartCount);
        Assert.Equal(1, source.StopCount);
        Assert.Equal(EngineState.Stopped, engine.State);
    }

    private sealed class FakeDeviceProvider(OutputDeviceInfo device) : IAudioOutputDeviceProvider
    {
        public IReadOnlyList<OutputDeviceInfo> GetActiveDevices() => [device];

        public OutputDeviceInfo? GetDefaultDevice() => device;
    }

    private sealed class FakeCaptureFactory : IAudioCaptureFactory
    {
        public IAudioCapture Create(OutputDeviceInfo device) => throw new NotSupportedException();
    }

    private sealed class FakeAudioSource : IAudioSource
    {
        public event EventHandler<AudioSamplesAvailableEventArgs>? SamplesAvailable;

        public event EventHandler<AudioSourceStateChangedEventArgs>? StateChanged;

        public event EventHandler<CaptureFaultedEventArgs>? Faulted;

        public AudioSourceState State { get; private set; } = AudioSourceState.Stopped;

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Publish(float[] samples, AudioFormat format) =>
            SamplesAvailable?.Invoke(this, new AudioSamplesAvailableEventArgs(samples, format));

        public void PublishFault(Exception exception) =>
            Faulted?.Invoke(this, new CaptureFaultedEventArgs(exception));

        public void PublishState(AudioSourceState state)
        {
            State = state;
            StateChanged?.Invoke(this, new AudioSourceStateChangedEventArgs(state));
        }
    }
}
