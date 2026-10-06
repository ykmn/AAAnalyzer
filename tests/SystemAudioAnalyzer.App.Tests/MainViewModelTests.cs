using SystemAudioAnalyzer.App.Services;
using SystemAudioAnalyzer.App.ViewModels;
using SystemAudioAnalyzer.Core;

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

    private static MainViewModel CreateViewModel() =>
        new(new FakeAnalyzerController(), [new OutputDeviceInfo("default", "Speakers", true)]);

    private sealed class FakeAnalyzerController : IAnalyzerController
    {
        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

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
    }
}
