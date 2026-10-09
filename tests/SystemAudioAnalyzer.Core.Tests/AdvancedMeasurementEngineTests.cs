namespace SystemAudioAnalyzer.Core.Tests;

public sealed class AdvancedMeasurementEngineTests
{
    [Fact]
    public async Task ResetTruePeakClearsOnlyTheRequestedChannel()
    {
        var source = new FakeSource();
        var device = new OutputDeviceInfo("default", "Speakers", true);
        await using var engine = new AudioAnalysisEngine(new FakeDeviceProvider(device), new UnsupportedCaptureFactory());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await engine.StartAsync(source);
        source.Publish([1.1f, 0.5f, 0.5f, 0.5f], new AudioFormat(48_000, 2));
        await using var frames = engine.ReadFrames(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        Assert.True(await frames.MoveNextAsync());

        engine.ResetTruePeak(channel: 0);
        await Task.Delay(40, cancellation.Token);
        source.Publish([0.4f, 0.4f], new AudioFormat(48_000, 2));
        Assert.True(await frames.MoveNextAsync());

        var truePeak = Assert.IsType<AdvancedMeasurementFrame>(frames.Current.AdvancedMeasurements).TruePeak;
        Assert.InRange(truePeak.Maximum[0], 0.39f, 0.41f);
        Assert.True(truePeak.Maximum[1] >= 0.5f);
    }

    [Fact]
    public async Task ResetLoudnessDoesNotResetTruePeakMeasurements()
    {
        var source = new FakeSource();
        var device = new OutputDeviceInfo("default", "Speakers", true);
        await using var engine = new AudioAnalysisEngine(new FakeDeviceProvider(device), new UnsupportedCaptureFactory());
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await engine.StartAsync(source);
        source.Publish([0.8f, 0.2f], new AudioFormat(48_000, 2));
        await using var frames = engine.ReadFrames(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        Assert.True(await frames.MoveNextAsync());

        engine.ResetLoudness();
        await Task.Delay(40, cancellation.Token);
        source.Publish([0.4f, 0.1f], new AudioFormat(48_000, 2));
        Assert.True(await frames.MoveNextAsync());

        var truePeak = Assert.IsType<AdvancedMeasurementFrame>(frames.Current.AdvancedMeasurements).TruePeak;
        Assert.True(truePeak.Maximum[0] >= 0.8f);
    }

    private sealed class FakeDeviceProvider(OutputDeviceInfo device) : IAudioOutputDeviceProvider
    {
        public IReadOnlyList<OutputDeviceInfo> GetActiveDevices() => [device];

        public OutputDeviceInfo? GetDefaultDevice() => device;
    }

    private sealed class UnsupportedCaptureFactory : IAudioCaptureFactory
    {
        public IAudioCapture Create(OutputDeviceInfo device, FaderMode faderMode = default) => throw new NotSupportedException();
    }

    private sealed class FakeSource : IAudioSource
    {
        public event EventHandler<AudioSamplesAvailableEventArgs>? SamplesAvailable;
        public event EventHandler<AudioSourceStateChangedEventArgs>? StateChanged;
        public event EventHandler<CaptureFaultedEventArgs>? Faulted;
        public AudioSourceState State { get; private set; } = AudioSourceState.Stopped;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            State = AudioSourceState.Running;
            StateChanged?.Invoke(this, new AudioSourceStateChangedEventArgs(State));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            State = AudioSourceState.Stopped;
            StateChanged?.Invoke(this, new AudioSourceStateChangedEventArgs(State));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Publish(float[] samples, AudioFormat format) =>
            SamplesAvailable?.Invoke(this, new AudioSamplesAvailableEventArgs(samples, format));

        public void PublishFault(Exception exception) => Faulted?.Invoke(this, new CaptureFaultedEventArgs(exception));
    }
}
