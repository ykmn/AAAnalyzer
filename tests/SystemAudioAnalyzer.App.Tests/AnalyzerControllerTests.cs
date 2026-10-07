using SystemAudioAnalyzer.App.Services;
using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class AnalyzerControllerTests
{
    [Fact]
    public async Task ConfigurationUpdateChangesPublishedFftWithoutRestartingSource()
    {
        var source = new FakeSource();
        await using var controller = CreateController(_ => source);
        IAnalyzerController api = controller;
        var initialFrame = new TaskCompletionSource<AnalysisFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updatedFrame = new TaskCompletionSource<AnalysisFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.FrameAvailable += (_, frame) =>
        {
            if (frame.Spectrum?.FftSize == 4096) initialFrame.TrySetResult(frame);
            if (frame.Spectrum?.FftSize == 512) updatedFrame.TrySetResult(frame);
        };
        await controller.StartAsync(new SourceSelection(SourceMode.Device, new OutputDeviceInfo("default", "Speakers", true), null));
        source.PublishSamples(new float[4096], new AudioFormat(48_000, 1));
        await initialFrame.Task.WaitAsync(TimeSpan.FromSeconds(2));

        api.SetAnalysisConfiguration(new AnalysisConfiguration(512, SpectrumWindow.Rectangular));
        await Task.Delay(40);
        source.PublishSamples(Enumerable.Repeat(1f, 512).ToArray(), new AudioFormat(48_000, 1));
        var frame = await updatedFrame.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.InRange(frame.Spectrum!.Magnitudes[0], 0.999f, 1.001f);
        Assert.Equal(AudioSourceState.Running, source.State);
        Assert.Equal(1, source.StartCount);
        Assert.Equal(0, source.StopCount);
    }

    [Fact]
    public async Task SourceChangeStopsThePreviousSourceBeforeStartingTheNext()
    {
        var first = new FakeSource();
        var second = new FakeSource();
        var index = 0;
        await using var controller = CreateController(_ => index++ == 0 ? first : second);
        var device = new OutputDeviceInfo("default", "Speakers", true);

        await controller.StartAsync(new SourceSelection(SourceMode.Device, device, null));
        await controller.StartAsync(new SourceSelection(SourceMode.Device, device, null));

        Assert.Equal(1, first.StopCount);
        Assert.Equal(1, second.StartCount);
    }

    [Fact]
    public async Task FaultLeavesTheControllerReadyForManualRestart()
    {
        var faulty = new FakeSource();
        var retry = new FakeSource();
        var index = 0;
        await using var controller = CreateController(_ => index++ == 0 ? faulty : retry);
        var device = new OutputDeviceInfo("default", "Speakers", true);

        await controller.StartAsync(new SourceSelection(SourceMode.Device, device, null));
        faulty.PublishFault(new InvalidOperationException("stream failed"));
        await controller.StartAsync(new SourceSelection(SourceMode.Device, device, null));

        Assert.Equal(1, retry.StartCount);
    }

    private static AnalyzerController CreateController(Func<SourceSelection, IAudioSource> sourceFactory)
    {
        var device = new OutputDeviceInfo("default", "Speakers", true);
        return new AnalyzerController(
            new AudioAnalysisEngine(new FakeDeviceProvider(device), new UnsupportedCaptureFactory()),
            sourceFactory);
    }

    private sealed class FakeDeviceProvider(OutputDeviceInfo device) : IAudioOutputDeviceProvider
    {
        public IReadOnlyList<OutputDeviceInfo> GetActiveDevices() => [device];

        public OutputDeviceInfo? GetDefaultDevice() => device;
    }

    private sealed class UnsupportedCaptureFactory : IAudioCaptureFactory
    {
        public IAudioCapture Create(OutputDeviceInfo device) => throw new NotSupportedException();
    }

    private sealed class FakeSource : IAudioSource
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
            State = AudioSourceState.Running;
            StateChanged?.Invoke(this, new AudioSourceStateChangedEventArgs(State));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            State = AudioSourceState.Stopped;
            StateChanged?.Invoke(this, new AudioSourceStateChangedEventArgs(State));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void PublishSamples(float[] samples, AudioFormat format) =>
            SamplesAvailable?.Invoke(this, new AudioSamplesAvailableEventArgs(samples, format));

        public void PublishFault(Exception exception) => Faulted?.Invoke(this, new CaptureFaultedEventArgs(exception));
    }
}
