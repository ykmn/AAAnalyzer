namespace SystemAudioAnalyzer.Core;

public sealed record LoudnessMeasurement(float? MomentaryLufs, float? ShortTermLufs, float? IntegratedLufs);
