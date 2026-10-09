namespace SystemAudioAnalyzer.Core.Tests;

public sealed class EngineNotificationsTests
{
    [Fact]
    public async Task SwitchDeviceRestartsCaptureAndPublishesDeviceChangedDiagnostic()
    {
        var firstDevice = new OutputDeviceInfo("first", "Speakers", IsDefault: true);
        var secondDevice = new OutputDeviceInfo("second", "Headphones", IsDefault: false);
        var factory = new RecordingCaptureFactory();
        await using var engine = new AudioAnalysisEngine(new FakeDeviceProvider(firstDevice), factory);
        var diagnostics = new List<EngineDiagnostic>();
        engine.DiagnosticPublished += (_, diagnostic) => diagnostics.Add(diagnostic);

        await engine.StartAsync();
        await engine.SwitchDeviceAsync(secondDevice);

        Assert.Equal(new[] { firstDevice, secondDevice }, factory.RequestedDevices);
        Assert.Equal(secondDevice, engine.CurrentDevice);
        Assert.Contains(diagnostics, diagnostic =>
            diagnostic.Kind == EngineDiagnosticKind.DeviceChanged && diagnostic.Device == secondDevice);
    }

    private sealed class FakeDeviceProvider(OutputDeviceInfo device) : IAudioOutputDeviceProvider
    {
        public IReadOnlyList<OutputDeviceInfo> GetActiveDevices() => [device];

        public OutputDeviceInfo? GetDefaultDevice() => device;
    }

    private sealed class RecordingCaptureFactory : IAudioCaptureFactory
    {
        public List<OutputDeviceInfo> RequestedDevices { get; } = [];

        public IAudioCapture Create(OutputDeviceInfo device, FaderMode faderMode = default)
        {
            RequestedDevices.Add(device);
            return new FakeCapture();
        }
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
