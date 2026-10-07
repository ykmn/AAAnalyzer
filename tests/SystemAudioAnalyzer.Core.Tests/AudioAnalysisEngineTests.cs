namespace SystemAudioAnalyzer.Core.Tests;

public sealed class AudioAnalysisEngineTests
{
    [Fact]
    public async Task LiveUpdatesAdoptLatestConfigurationWithoutRestartingOrKeepingOldFftSamples()
    {
        var capture = new FakeCapture();
        var device = new OutputDeviceInfo("default", "Speakers", true);
        await using var engine = new AudioAnalysisEngine(new FakeDeviceProvider(device), new FakeCaptureFactory(capture));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await engine.StartAsync();
        await using var frames = engine.ReadFrames(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var format = new AudioFormat(48_000, 2);

        capture.Publish(Enumerable.Repeat(1f, 8192).ToArray(), format);
        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(4096, frames.Current.Spectrum!.FftSize);

        // Consecutive requests before the next buffer must select the latest one.
        engine.SetAnalysisConfiguration(new AnalysisConfiguration(1024, SpectrumWindow.Hamming));
        engine.SetAnalysisConfiguration(new AnalysisConfiguration(512, SpectrumWindow.Rectangular));
        Assert.Equal(EngineState.Running, engine.State);
        await Task.Delay(40, cancellation.Token);
        capture.Publish(new float[1024], format);
        Assert.True(await frames.MoveNextAsync());
        var updated = frames.Current;
        Assert.NotNull(updated.Spectrum);
        Assert.Equal(512, updated.Spectrum!.FftSize);
        Assert.Equal(257, updated.Spectrum.Magnitudes.Count);
        Assert.Equal(93.75f, updated.Spectrum.GetFrequencyHz(1));
        Assert.All(updated.Spectrum.Magnitudes, value => Assert.Equal(0f, value));
        Assert.Equal(512, updated.AdvancedMeasurements!.StereoSpectrum!.Left.FftSize);
        Assert.Equal(512, updated.AdvancedMeasurements.StereoSpectrum.Right.FftSize);
        Assert.True(updated.AdvancedMeasurements.TruePeak.Maximum[0] > 0.9f);

        // Changing only the window also discards the preceding FFT overlap.
        engine.SetAnalysisConfiguration(new AnalysisConfiguration(512, SpectrumWindow.Hamming));
        await Task.Delay(40, cancellation.Token);
        capture.Publish(Enumerable.Repeat(1f, 1024).ToArray(), format);
        Assert.True(await frames.MoveNextAsync());
        Assert.InRange(frames.Current.Spectrum!.Magnitudes[0], 276.018f, 276.022f);

        engine.SetAnalysisConfiguration(new AnalysisConfiguration(2048, SpectrumWindow.Blackman));
        await Task.Delay(40, cancellation.Token);
        capture.Publish(new float[4096], format);
        Assert.True(await frames.MoveNextAsync());
        Assert.Equal(2048, frames.Current.Spectrum!.FftSize);
        Assert.Equal(EngineState.Running, engine.State);
        Assert.Equal(1, capture.StartCount);
        Assert.Equal(0, capture.StopCount);
    }

    [Fact]
    public async Task ConfigurationRequestedBeforeStartAppliesToFirstMonoFrame()
    {
        var capture = new FakeCapture();
        var device = new OutputDeviceInfo("default", "Speakers", true);
        await using var engine = new AudioAnalysisEngine(new FakeDeviceProvider(device), new FakeCaptureFactory(capture));
        engine.SetAnalysisConfiguration(new AnalysisConfiguration(512, SpectrumWindow.Rectangular));
        await engine.StartAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var frames = engine.ReadFrames(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        capture.Publish(Enumerable.Repeat(1f, 512).ToArray(), new AudioFormat(48_000, 1));
        Assert.True(await frames.MoveNextAsync());
        Assert.NotNull(frames.Current.Spectrum);
        Assert.Equal(512, frames.Current.Spectrum!.FftSize);
        Assert.Equal(512f, frames.Current.Spectrum.Magnitudes[0]);
    }

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
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public event EventHandler<AudioSamplesAvailableEventArgs>? SamplesAvailable;

        public event EventHandler<CaptureFaultedEventArgs>? Faulted;

        public void Start()
        {
            StartCount++;
        }

        public void Stop()
        {
            StopCount++;
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
