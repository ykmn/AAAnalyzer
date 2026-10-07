using SystemAudioAnalyzer.App.Rendering;
using SystemAudioAnalyzer.App.Services;
using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.Core;

namespace SystemAudioAnalyzer.App.Tests;

public sealed class WorkspaceLayoutTests
{
    [Fact]
    public void WorkspaceKeepsThePeakRailAtOneHundredThirtyPixels()
    {
        Assert.Equal(130, WorkspaceLayout.PeakRailWidth);
    }

    [Fact]
    public void SelectingAnInstrumentMakesOnlyThatTabActive()
    {
        var viewModel = new MainViewModel(new FakeController(), []);

        viewModel.SelectTab(InstrumentTab.Loudness);

        Assert.Equal(InstrumentTab.Loudness, viewModel.ActiveTab);
    }

    private sealed class FakeController : IAnalyzerController
    {
        public event EventHandler<AnalysisFrame>? FrameAvailable { add { } remove { } }
        public event EventHandler<AudioSourceStateChangedEventArgs>? SourceStateChanged { add { } remove { } }
        public Task StartAsync(SourceSelection selection, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ResetTruePeak(int channel) { }
        public void ResetTruePeakMaximum(int channel) { }
        public void ResetTruePeakOverload(int channel) { }
        public void ResetLoudness() { }
        public void SetAnalysisConfiguration(AnalysisConfiguration configuration) { }
        public void SetLoudnessIntegratedWindow(int seconds) { }
    }
}
