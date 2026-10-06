namespace SystemAudioAnalyzer.Core;

public sealed class EngineStateMachine
{
    public EngineState State { get; private set; } = EngineState.Stopped;

    public void BeginStarting()
    {
        if (State is not EngineState.Stopped and not EngineState.Faulted)
        {
            throw new InvalidOperationException($"Cannot start an engine in the {State} state.");
        }

        State = EngineState.Starting;
    }

    public void MarkRunning()
    {
        if (State != EngineState.Starting)
        {
            throw new InvalidOperationException($"Cannot mark an engine in the {State} state as running.");
        }

        State = EngineState.Running;
    }

    public void BeginStopping()
    {
        if (State is not EngineState.Running and not EngineState.Faulted)
        {
            throw new InvalidOperationException($"Cannot stop an engine in the {State} state.");
        }

        State = EngineState.Stopping;
    }

    public void MarkStopped()
    {
        if (State != EngineState.Stopping)
        {
            throw new InvalidOperationException($"Cannot mark an engine in the {State} state as stopped.");
        }

        State = EngineState.Stopped;
    }

    public void MarkFaulted()
    {
        State = EngineState.Faulted;
    }
}
