using SystemAudioAnalyzer.App.Services;
using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.Core;
using SystemAudioAnalyzer.App.Settings;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class MainViewModelTests
{
    [Theory]
    [InlineData("https://radio.example/live")]
    [InlineData("http://radio.example/live.m3u8")]
    public void StreamModeEnablesStartForHttpUrls(string value)
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedSourceMode = SourceMode.Stream;
        viewModel.StreamUrl = value;

        Assert.True(viewModel.CanStart);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://radio.example/live")]
    [InlineData("not a url")]
    public void StreamModeDisablesStartForInvalidUrls(string value)
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedSourceMode = SourceMode.Stream;
        viewModel.StreamUrl = value;

        Assert.False(viewModel.CanStart);
    }

    [Fact]
    public async Task StartAndStopCommandsFollowControllerState()
    {
        var controller = new FakeAnalyzerController();
        var viewModel = new MainViewModel(controller, [new OutputDeviceInfo("default", "Speakers", true)]);

        await viewModel.StartCommand.ExecuteAsync();
        Assert.True(viewModel.IsAnalyzing);
        Assert.Equal(1, controller.StartCount);

        await viewModel.StopCommand.ExecuteAsync();
        Assert.False(viewModel.IsAnalyzing);
        Assert.Equal(1, controller.StopCount);
    }

    [Fact]
    public async Task StartCommandResumesOnTheCapturedSynchronizationContext()
    {
        var previousContext = SynchronizationContext.Current;
        var synchronizationContext = new RecordingSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(synchronizationContext);
        try
        {
            var controller = new DelayedAnalyzerController();
            var viewModel = new MainViewModel(controller, [new OutputDeviceInfo("default", "Speakers", true)]);
            var startTask = viewModel.StartCommand.ExecuteAsync();

            controller.CompleteStart();
            Assert.True(SpinWait.SpinUntil(() => synchronizationContext.PendingCount > 0, TimeSpan.FromSeconds(1)));
            synchronizationContext.RunAll();
            await startTask;

            Assert.True(viewModel.IsAnalyzing);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public void RtaPresentationSettingsCanBeChangedIndependentlyOfAnalysis()
    {
        var viewModel = CreateViewModel();

        viewModel.RtaResolution = RtaResolution.OneTwelfth;
        viewModel.RtaChannelMode = RtaChannelMode.Right;

        Assert.Equal(RtaResolution.OneTwelfth, viewModel.RtaResolution);
        Assert.Equal(RtaChannelMode.Right, viewModel.RtaChannelMode);
        Assert.True(viewModel.IsRtaRight);
        Assert.False(viewModel.IsRtaMono);
        Assert.False(viewModel.IsAnalyzing);
    }

    [Fact]
    public void ResetAllMeasurementsForwardsToTheController()
    {
        var controller = new FakeAnalyzerController();
        var viewModel = new MainViewModel(controller, [new OutputDeviceInfo("default", "Speakers", true)]);

        viewModel.ResetAllMeasurements();

        Assert.Equal(new[] { 0, 1 }, controller.ResetTruePeakChannels);
        Assert.Equal(1, controller.ResetLoudnessCount);
    }

    [Fact]
    public void ApplyingMeasurementSettingsUpdatesRtaControls()
    {
        var viewModel = CreateViewModel();
        var settings = MeasurementSettings.Default with
        {
            Rta = MeasurementSettings.Default.Rta with { Resolution = RtaResolution.OneTwelfth, Source = RtaChannelMode.Right },
        };

        viewModel.MeasurementSettings = settings;

        Assert.Equal(RtaResolution.OneTwelfth, viewModel.RtaResolution);
        Assert.Equal(RtaChannelMode.Right, viewModel.RtaChannelMode);
        Assert.True(viewModel.IsRtaRight);
        Assert.False(viewModel.IsRtaMono);
    }

    [Fact]
    public void ApplyingMeasurementSettingsUpdatesIntegratedLoudnessWindow()
    {
        var controller = new FakeAnalyzerController();
        var viewModel = new MainViewModel(controller, [new OutputDeviceInfo("default", "Speakers", true)]);

        viewModel.MeasurementSettings = MeasurementSettings.Default with
        {
            Meters = MeasurementSettings.Default.Meters with { IntegratedWindowSeconds = 120 },
        };

        Assert.Equal(120, controller.IntegratedWindowSeconds);
    }

    [Fact]
    public void ApplyingMeasurementSettingsForwardsFftAndWindowWithoutRestartingSource()
    {
        var controller = new FakeAnalyzerController();
        var viewModel = new MainViewModel(controller, [new OutputDeviceInfo("default", "Speakers", true)]);
        var settings = MeasurementSettings.Default with
        {
            Analyzer = MeasurementSettings.Default.Analyzer with
            {
                FftSize = 8192,
                WindowFunction = AnalyzerWindowFunction.Hamming,
            },
        };

        viewModel.MeasurementSettings = settings;

        Assert.Equal(new AnalysisConfiguration(8192, SpectrumWindow.Hamming), controller.LastAnalysisConfiguration);
        Assert.Equal(0, controller.StartCount);
        Assert.Equal(0, controller.StopCount);
    }

    [Fact]
    public void ToolbarMethodsUpdateSettingsAndNotifyDependentText()
    {
        var viewModel = CreateViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        viewModel.AdjustRtaAveraging(10);
        viewModel.ZoomLoudness(2);
        viewModel.LoudnessMetric = LoudnessMetric.Momentary;

        Assert.Equal("Avg 60", viewModel.RtaAverageText);
        Assert.Equal("-17..-5", viewModel.LoudnessScaleText);
        Assert.Equal(LoudnessMetric.Momentary, viewModel.MeasurementSettings.Loudness.Metric);
        Assert.Contains(nameof(MainViewModel.RtaAverageText), changed);
        Assert.Contains(nameof(MainViewModel.LoudnessScaleText), changed);
    }

    private static MainViewModel CreateViewModel() =>
        new(new FakeAnalyzerController(), [new OutputDeviceInfo("default", "Speakers", true)]);

    private sealed class FakeAnalyzerController : IAnalyzerController
    {
        public event EventHandler<AnalysisFrame>? FrameAvailable;

        public event EventHandler<AudioSourceStateChangedEventArgs>? SourceStateChanged;

        public int StartCount { get; private set; }

        public List<int> ResetTruePeakChannels { get; } = [];

        public int ResetLoudnessCount { get; private set; }

        public int StopCount { get; private set; }
        public int IntegratedWindowSeconds { get; private set; }
        public AnalysisConfiguration? LastAnalysisConfiguration { get; private set; }

        public Task StartAsync(SourceSelection selection, CancellationToken cancellationToken = default)
        {
            StartCount++;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public void ResetTruePeak(int channel) => ResetTruePeakChannels.Add(channel);
        public void ResetTruePeakMaximum(int channel) { }
        public void ResetTruePeakOverload(int channel) { }

        public void ResetLoudness() => ResetLoudnessCount++;

        public void SetAnalysisConfiguration(AnalysisConfiguration configuration) => LastAnalysisConfiguration = configuration;
        public void SetLoudnessIntegratedWindow(int seconds) => IntegratedWindowSeconds = seconds;

        public void PublishFrame(AnalysisFrame frame) => FrameAvailable?.Invoke(this, frame);

        public void PublishState(AudioSourceState state) =>
            SourceStateChanged?.Invoke(this, new AudioSourceStateChangedEventArgs(state));
    }

    private sealed class DelayedAnalyzerController : IAnalyzerController
    {
        private readonly TaskCompletionSource _startCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event EventHandler<AnalysisFrame>? FrameAvailable;

        public event EventHandler<AudioSourceStateChangedEventArgs>? SourceStateChanged;

        public Task StartAsync(SourceSelection selection, CancellationToken cancellationToken = default) => _startCompletion.Task;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void ResetTruePeak(int channel) { }
        public void ResetTruePeakMaximum(int channel) { }
        public void ResetTruePeakOverload(int channel) { }

        public void ResetLoudness() { }

        public void SetAnalysisConfiguration(AnalysisConfiguration configuration) { }
        public void SetLoudnessIntegratedWindow(int seconds) { }

        public void CompleteStart() => _startCompletion.SetResult();

        public void PublishFrame(AnalysisFrame frame) => FrameAvailable?.Invoke(this, frame);

        public void PublishState(AudioSourceState state) =>
            SourceStateChanged?.Invoke(this, new AudioSourceStateChangedEventArgs(state));
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public int PendingCount
        {
            get
            {
                lock (_callbacks)
                {
                    return _callbacks.Count;
                }
            }
        }

        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (_callbacks)
            {
                _callbacks.Enqueue((callback, state));
            }
        }

        public void RunAll()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) work;
                lock (_callbacks)
                {
                    if (_callbacks.Count == 0)
                    {
                        return;
                    }

                    work = _callbacks.Dequeue();
                }

                work.Callback(work.State);
            }
        }
    }
}
