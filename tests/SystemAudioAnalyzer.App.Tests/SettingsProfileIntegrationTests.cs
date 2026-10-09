using SystemAudioAnalyzer.App.Services;
using SystemAudioAnalyzer.App.Settings;
using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class SettingsProfileIntegrationTests
{
    [Fact]
    public async Task DefaultProfileAppliesLiveSavesExplicitlyAndSurvivesStoreRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AAAnalyzerTests", Guid.NewGuid().ToString("N"));
        var legacyPath = Path.Combine(directory, "legacy.json");
        var store = new SettingsStore(directory, null, legacyPath);
        var initial = MeasurementSettings.Default with
        {
            Analyzer = MeasurementSettings.Default.Analyzer with { FftSize = 512 },
        };
        var catalog = SettingsProfileCatalog.Default.SaveProfile("default", initial)
            .SaveAsProfile("Studio", MeasurementSettings.Default with { Phase = new PhaseDisplaySettings(2) });
        await store.SaveCatalogAsync(catalog);

        var startupStore = new SettingsStore(directory, null, legacyPath);
        var startupSettings = await startupStore.LoadStartupSettingsAsync();
        Assert.Equal(initial, startupSettings);
        var source = new FakeSource();
        await using var controller = CreateController(_ => source);
        var mainViewModel = new MainViewModel(controller, [new OutputDeviceInfo("default", "Speakers", true)])
        {
            MeasurementSettings = startupSettings,
        };
        var startupFrame = NewFrameSource(controller, 512);
        await mainViewModel.StartCommand.ExecuteAsync();
        source.PublishSamples(Enumerable.Repeat(1f, 512).ToArray(), new AudioFormat(48_000, 1));
        await startupFrame.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var dialog = new SettingsDialogViewModel(startupStore, await startupStore.LoadCatalogAsync(),
            mainViewModel.MeasurementSettings, InstrumentTab.Analyzer);
        dialog.AnalyzerFftSize = 1024;
        dialog.Applied += settings => mainViewModel.MeasurementSettings = settings;
        var applied = dialog.Apply();
        Assert.Equal(applied, mainViewModel.MeasurementSettings);
        var reconfiguredFrame = NewFrameSource(controller, 1024);
        await Task.Delay(40); // The engine adopts the latest FFT configuration on its worker boundary.
        source.PublishSamples(Enumerable.Repeat(0.5f, 1024).ToArray(), new AudioFormat(48_000, 1));
        var frame = await reconfiguredFrame.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1024, frame.Spectrum!.FftSize);
        Assert.Equal(AudioSourceState.Running, source.State);
        Assert.Equal(1, source.StartCount);
        Assert.Equal(0, source.StopCount);

        await dialog.SaveAsync();
        var restartedStore = new SettingsStore(directory, null, legacyPath);
        var persistedCatalog = await restartedStore.LoadCatalogAsync();
        Assert.Equal(catalog.DefaultProfileId, persistedCatalog.DefaultProfileId);
        Assert.Equal(2, persistedCatalog.Profiles.Count);
        Assert.Equal(applied, await restartedStore.LoadStartupSettingsAsync());
        Assert.Equal(1024, persistedCatalog.Profiles.Single(profile => profile.Id == catalog.DefaultProfileId).Settings.Analyzer.FftSize);
        Assert.Equal(2, persistedCatalog.Profiles.Single(profile => profile.Name == "Studio").Settings.Phase.Gain);
    }

    private static TaskCompletionSource<AnalysisFrame> NewFrameSource(AnalyzerController controller, int fftSize)
    {
        var result = new TaskCompletionSource<AnalysisFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.FrameAvailable += (_, frame) =>
        {
            if (frame.Spectrum?.FftSize == fftSize)
                result.TrySetResult(frame);
        };
        return result;
    }

    private static AnalyzerController CreateController(Func<SourceSelection, IAudioSource> sourceFactory)
    {
        var device = new OutputDeviceInfo("default", "Speakers", true);
        return new AnalyzerController(
            new AudioAnalysisEngine(new FakeDeviceProvider(device), new UnsupportedCaptureFactory()), sourceFactory);
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
