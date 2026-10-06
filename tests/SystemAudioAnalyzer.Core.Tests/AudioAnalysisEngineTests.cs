namespace SystemAudioAnalyzer.Core.Tests;

public sealed class AudioAnalysisEngineTests
{
    [Fact]
    public async Task StartPublishesLevelsFromTheCaptureSource()
    {
        var capture = new FakeCapture();
        var device = new OutputDeviceInfo("default", "Speakers", IsDefault: true);
        await using var engine = new AudioAnalysisEngine(new FakeDeviceProvider(device), new FakeCaptureFactory(capture));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await engine.StartAsync();
        capture.Publish(new[] { 0.5f, -0.25f, -0.5f, 0.25f }, new AudioFormat(48_000, channels: 2));

        await using var frames = engine.ReadFrames(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        Assert.True(await frames.MoveNextAsync());
        var frame = frames.Current;

        Assert.Equal(EngineState.Running, engine.State);
        Assert.Equal(0.5f, frame.Levels[0].Peak);
        Assert.Equal(0.25f, frame.Levels[1].Peak);
        Assert.Null(frame.Spectrum);

        await engine.StopAsync();
        Assert.Equal(EngineState.Stopped, engine.State);
    }

    private sealed class FakeDeviceProvider(OutputDeviceInfo device) : IAudioOutputDeviceProvider
    {
        public IReadOnlyList<OutputDeviceInfo> GetActiveDevices() => [device];

        public OutputDeviceInfo? GetDefaultDevice() => device;
    }

    private sealed class FakeCaptureFactory(FakeCapture capture) : IAudioCaptureFactory
    {
        public IAudioCapture Create(OutputDeviceInfo device) => capture;
    }

    private sealed class FakeCapture : IAudioCapture
    {
        public event EventHandler<AudioSamplesAvailableEventArgs>? SamplesAvailable;

        public event EventHandler<CaptureFaultedEventArgs>? Faulted;

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void Dispose()
        {
        }

        public void Publish(float[] samples, AudioFormat format) =>
            SamplesAvailable?.Invoke(this, new AudioSamplesAvailableEventArgs(samples, format));

        public void PublishFault(Exception exception) =>
            Faulted?.Invoke(this, new CaptureFaultedEventArgs(exception));
    }
}
