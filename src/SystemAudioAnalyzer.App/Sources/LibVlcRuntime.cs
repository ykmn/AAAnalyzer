using LibVLCSharp.Shared;

namespace SystemAudioAnalyzer.App.Sources;

/// <summary>One shared LibVLC instance, loaded in the background at startup so the first stream does not pay for it.</summary>
public static class LibVlcRuntime
{
    private static readonly Lazy<Task<LibVLC>> Instance = new(() => Task.Run(() =>
    {
        LibVLCSharp.Shared.Core.Initialize();
        return new LibVLC();
    }));

    public static Task<LibVLC> GetAsync() => Instance.Value;

    public static void WarmUp(Action<Exception>? onFailure = null) =>
        Instance.Value.ContinueWith(task => onFailure?.Invoke(task.Exception!.GetBaseException()), TaskContinuationOptions.OnlyOnFaulted);
}
