namespace SystemAudioAnalyzer.App.Rendering;

/// <summary>BGRA pixel buffer for one waterfall channel; new rows enter at the top and older rows move down.</summary>
public sealed class WaterfallBitmapBuffer
{
    private static readonly TimeSpan MaxRowGap = TimeSpan.FromSeconds(1.5);

    private readonly TimeSpan _window;
    private readonly uint _background;
    private DateTimeOffset? _last;
    private double _carry;

    public WaterfallBitmapBuffer(int width, int height, TimeSpan window, uint background)
    {
        if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 1) throw new ArgumentOutOfRangeException(nameof(height));
        if (window <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(window));
        Width = width;
        Height = height;
        _window = window;
        _background = background;
        Pixels = new uint[width * height];
        Array.Fill(Pixels, background);
    }

    public int Width { get; }

    public int Height { get; }

    public uint[] Pixels { get; }

    /// <summary>Adds a row; returns true when older rows moved down, false when the top row was only refreshed.</summary>
    public bool Append(DateTimeOffset timestamp, ReadOnlySpan<uint> row)
    {
        if (row.Length != Width) throw new ArgumentException("Row length must equal the buffer width.", nameof(row));
        var scroll = 1;
        var scrolled = true;
        var paused = false;
        if (_last is { } last)
        {
            paused = timestamp - last > MaxRowGap;
            _carry += Math.Max(0d, (timestamp - last).TotalSeconds) / _window.TotalSeconds * Height;
            scroll = (int)Math.Floor(_carry);
            if (scroll < 1)
            {
                scroll = 0;
                scrolled = false;
            }
            else
            {
                _carry -= scroll;
            }
        }

        _last = timestamp;
        if (scroll > 0)
        {
            scroll = Math.Min(scroll, Height);
            Array.Copy(Pixels, 0, Pixels, scroll * Width, (Height - scroll) * Width);
        }

        for (var line = 0; line < Math.Max(1, scroll); line++)
        {
            // After a pause (analysis stopped) only the newest line carries data; the gap stays background.
            if (paused && line > 0) Pixels.AsSpan(line * Width, Width).Fill(_background);
            else row.CopyTo(Pixels.AsSpan(line * Width, Width));
        }

        return scrolled;
    }

    public void Clear()
    {
        Array.Fill(Pixels, _background);
        _last = null;
        _carry = 0;
    }
}
