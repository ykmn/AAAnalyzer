namespace SystemAudioAnalyzer.Core.Tests;

public sealed class AudioAnalysisEngineTests
{
    // Large buffers are cut into slices, each published as its own frame; the spectrum appears once a window is full.
    private static async Task<bool> NextWithSpectrumAsync(IAsyncEnumerator<AnalysisFrame> frames)
    {
        while (await frames.MoveNextAsync())
        {
            if (frames.Current.Spectrum is not null) return true;
        }

        return false;
    }

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
        Assert.True(await NextWithSpectrumAsync(frames));
        Assert.Equal(4096, frames.Current.Spectrum!.FftSize);

        // Consecutive requests before the next buffer must select the latest one.
        engine.SetAnalysisConfiguration(new AnalysisConfiguration(1024, SpectrumWindow.Hamming));
        engine.SetAnalysisConfiguration(new AnalysisConfiguration(512, SpectrumWindow.Rectangular));
        Assert.Equal(EngineState.Running, engine.State);
        await Task.Delay(40, cancellation.Token);
        capture.Publish(new float[1024], format);
        Assert.True(await NextWithSpectrumAsync(frames));
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
        Assert.True(await NextWithSpectrumAsync(frames));
        Assert.InRange(frames.Current.Spectrum!.Magnitudes[0], 0.999f, 1.001f);

        engine.SetAnalysisConfiguration(new AnalysisConfiguration(2048, SpectrumWindow.Blackman));
        await Task.Delay(40, cancellation.Token);
        capture.Publish(new float[4096], format);
        Assert.True(await NextWithSpectrumAsync(frames));
        Assert.Equal(2048, frames.Current.Spectrum!.FftSize);
        Assert.Equal(EngineState.Running, engine.State);
        Assert.Equal(1, capture.StartCount);
        Assert.Equal(0, capture.StopCount);
    }

    [Fact]
    public async Task ABurstOfBuffersIsPlayedOutAsEvenFramesWithoutDroppingAudio()
    {
        var capture = new FakeCapture();
        var device = new OutputDeviceInfo("default", "Speakers", true);
        await using var engine = new AudioAnalysisEngine(new FakeDeviceProvider(device), new FakeCaptureFactory(capture));
        var dropped = 0L;
        engine.DiagnosticPublished += (_, diagnostic) => Interlocked.Add(ref dropped, diagnostic.DroppedBufferCount);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await engine.StartAsync();
        var format = new AudioFormat(48_000, 2);

        // Network sources hand over about half a second at once: 20 buffers of 2048 frames.
        for (var index = 0; index < 20; index++) capture.Publish(new float[2048 * 2], format);

        var timestamps = new List<DateTimeOffset>();
        await foreach (var frame in engine.ReadFrames(cancellation.Token))
        {
            timestamps.Add(frame.Timestamp);
            if (timestamps.Count == 12) break;
        }

        Assert.Equal(0, dropped);
        var gaps = timestamps.Zip(timestamps.Skip(1), (first, second) => (second - first).TotalMilliseconds).ToArray();
        Assert.All(gaps, gap => Assert.InRange(gap, 5, 120));
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
        Assert.InRange(frames.Current.Spectrum.Magnitudes[0], 0.999f, 1.001f);
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

    [Fact]
    public async Task MetersSeeEveryBufferEvenWhenFramePublishingIsThrottled()
    {
        var capture = new FakeCapture();
        var device = new OutputDeviceInfo("default", "Speakers", true);
        await using var engine = new AudioAnalysisEngine(new FakeDeviceProvider(device), new FakeCaptureFactory(capture));
        await engine.StartAsync();
        var format = new AudioFormat(48_000, 2);
        float[] Buffer(float level) => Enumerable.Repeat(level, 960).ToArray();

        // A burst small enough for the sample queue (8) but far inside one throttle interval;
        // the loud buffer is neither the first (always published) nor the last.
        for (var index = 0; index < 6; index++)
        {
            capture.Publish(Buffer(index == 2 ? 0.9f : 0.1f), format);
        }

        await Task.Delay(120);
        capture.Publish(Buffer(0.05f), format);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(600));
        AnalysisFrame? last = null;
        try
        {
            await foreach (var frame in engine.ReadFrames(cancellation.Token)) last = frame;
        }
        catch (OperationCanceledException)
        {
        }

        Assert.NotNull(last);
        Assert.True(last!.AdvancedMeasurements!.TruePeak.Maximum[0] >= 0.89f, "The loud buffer must reach the true-peak meter.");
    }

    private sealed class FakeCaptureFactory(FakeCapture capture) : IAudioCaptureFactory
    {
        public IAudioCapture Create(OutputDeviceInfo device, FaderMode faderMode = default) => capture;
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
