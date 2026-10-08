using System.Globalization;
using SystemAudioAnalyzer.App.Localization;

namespace SystemAudioAnalyzer.App.ViewModels;

/// <summary>Rolling frame-rate, delivery-delay and dropped-buffer counters shown in the status bar.</summary>
public sealed class FrameStatistics
{
    private const double Smoothing = 0.2;
    private readonly Queue<DateTimeOffset> _arrivals = new();
    private double? _delayMilliseconds;
    private double? _uiWorkMilliseconds;

    public long DroppedBuffers { get; private set; }

    public double FramesPerSecond => _arrivals.Count;

    public double DelayMilliseconds => _delayMilliseconds ?? 0;

    public double UiWorkMilliseconds => _uiWorkMilliseconds ?? 0;

    /// <param name="frameTimestamp">When the engine produced the frame.</param>
    /// <param name="now">When the frame reached the UI.</param>
    /// <param name="droppedBuffers">Audio buffers the engine dropped for this frame.</param>
    /// <param name="uiWork">Time the UI spent applying the frame.</param>
    public void Record(DateTimeOffset frameTimestamp, DateTimeOffset now, long droppedBuffers, TimeSpan uiWork)
    {
        _arrivals.Enqueue(now);
        while (_arrivals.Count > 0 && now - _arrivals.Peek() > TimeSpan.FromSeconds(1)) _arrivals.Dequeue();
        _delayMilliseconds = Smooth(_delayMilliseconds, Math.Max(0, (now - frameTimestamp).TotalMilliseconds));
        _uiWorkMilliseconds = Smooth(_uiWorkMilliseconds, Math.Max(0, uiWork.TotalMilliseconds));
        DroppedBuffers += Math.Max(0, droppedBuffers);
    }

    public void Reset()
    {
        _arrivals.Clear();
        _delayMilliseconds = null;
        _uiWorkMilliseconds = null;
        DroppedBuffers = 0;
    }

    public string Text => string.Format(CultureInfo.InvariantCulture, Localizer.T("Diagnostics"),
        FramesPerSecond.ToString("0", CultureInfo.InvariantCulture), DelayMilliseconds.ToString("0", CultureInfo.InvariantCulture),
        UiWorkMilliseconds.ToString("0.0", CultureInfo.InvariantCulture), DroppedBuffers);

    private static double Smooth(double? previous, double value) =>
        previous is null ? value : (previous.Value * (1 - Smoothing)) + (value * Smoothing);
}
