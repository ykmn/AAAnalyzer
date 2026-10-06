namespace SystemAudioAnalyzer.Core.Tests;

public sealed class EngineStateMachineTests
{
    [Fact]
    public void StartMovesStoppedEngineToRunning()
    {
        var machine = new EngineStateMachine();

        machine.BeginStarting();
        machine.MarkRunning();

        Assert.Equal(EngineState.Running, machine.State);
    }

    [Fact]
    public void CannotStartAnAlreadyRunningEngine()
    {
        var machine = new EngineStateMachine();
        machine.BeginStarting();
        machine.MarkRunning();

        Assert.Throws<InvalidOperationException>(machine.BeginStarting);
    }
}
