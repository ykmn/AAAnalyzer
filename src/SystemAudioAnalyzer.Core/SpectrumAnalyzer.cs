namespace SystemAudioAnalyzer.Core;

public sealed class SpectrumAnalyzer
{
    private readonly int _fftSize;
    private readonly int _hopSize;
    private readonly List<float> _monoSamples = [];
    private AudioFormat? _format;

    public SpectrumAnalyzer(int fftSize = 4096)
    {
        if (fftSize < 2 || (fftSize & (fftSize - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fftSize), "FFT size must be a power of two.");
        }

        _fftSize = fftSize;
        _hopSize = fftSize / 4;
    }

    public bool TryProcess(ReadOnlySpan<float> interleavedSamples, AudioFormat format, out Spectrum? spectrum)
    {
        ArgumentNullException.ThrowIfNull(format);

        if (_format is not null && _format != format)
        {
            _monoSamples.Clear();
        }

        _format = format;
        var frameCount = interleavedSamples.Length / format.Channels;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var mixed = 0f;
            for (var channel = 0; channel < format.Channels; channel++)
            {
                mixed += interleavedSamples[(frame * format.Channels) + channel];
            }

            _monoSamples.Add(mixed / format.Channels);
        }

        if (_monoSamples.Count < _fftSize)
        {
            spectrum = null;
            return false;
        }

        var real = new float[_fftSize];
        var imaginary = new float[_fftSize];
        for (var index = 0; index < _fftSize; index++)
        {
            var window = 0.5f - (0.5f * MathF.Cos((2 * MathF.PI * index) / (_fftSize - 1)));
            real[index] = _monoSamples[index] * window;
        }

        Transform(real, imaginary);
        var magnitudes = new float[(_fftSize / 2) + 1];
        for (var index = 0; index < magnitudes.Length; index++)
        {
            magnitudes[index] = MathF.Sqrt((real[index] * real[index]) + (imaginary[index] * imaginary[index]));
        }

        _monoSamples.RemoveRange(0, _hopSize);
        spectrum = new Spectrum(format.SampleRate, _fftSize, magnitudes);
        return true;
    }

    private static void Transform(float[] real, float[] imaginary)
    {
        var length = real.Length;
        for (int index = 1, reversed = 0; index < length; index++)
        {
            var bit = length >> 1;
            for (; (reversed & bit) != 0; bit >>= 1)
            {
                reversed ^= bit;
            }

            reversed ^= bit;
            if (index < reversed)
            {
                (real[index], real[reversed]) = (real[reversed], real[index]);
                (imaginary[index], imaginary[reversed]) = (imaginary[reversed], imaginary[index]);
            }
        }

        for (var blockSize = 2; blockSize <= length; blockSize <<= 1)
        {
            var angle = -2 * MathF.PI / blockSize;
            var stepReal = MathF.Cos(angle);
            var stepImaginary = MathF.Sin(angle);
            for (var start = 0; start < length; start += blockSize)
            {
                var phaseReal = 1f;
                var phaseImaginary = 0f;
                for (var offset = 0; offset < blockSize / 2; offset++)
                {
                    var evenIndex = start + offset;
                    var oddIndex = evenIndex + (blockSize / 2);
                    var transformedReal = (phaseReal * real[oddIndex]) - (phaseImaginary * imaginary[oddIndex]);
                    var transformedImaginary = (phaseReal * imaginary[oddIndex]) + (phaseImaginary * real[oddIndex]);

                    real[oddIndex] = real[evenIndex] - transformedReal;
                    imaginary[oddIndex] = imaginary[evenIndex] - transformedImaginary;
                    real[evenIndex] += transformedReal;
                    imaginary[evenIndex] += transformedImaginary;

                    var nextPhaseReal = (phaseReal * stepReal) - (phaseImaginary * stepImaginary);
                    phaseImaginary = (phaseReal * stepImaginary) + (phaseImaginary * stepReal);
                    phaseReal = nextPhaseReal;
                }
            }
        }
    }
}
