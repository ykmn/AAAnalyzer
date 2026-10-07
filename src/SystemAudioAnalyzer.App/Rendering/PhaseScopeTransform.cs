namespace SystemAudioAnalyzer.App.Rendering;

public static class PhaseScopeTransform
{
    private const double InverseRootTwo = 0.7071067811865476;

    public static Point Transform(float left, float right, double gain)
    {
        if (gain <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(gain));
        }

        return new Point(
            (left - right) * gain * InverseRootTwo,
            (left + right) * gain * InverseRootTwo);
    }
}
