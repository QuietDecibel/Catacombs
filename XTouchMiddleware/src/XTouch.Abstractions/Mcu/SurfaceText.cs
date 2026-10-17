namespace XTouch.Abstractions.Mcu;

public static class SurfaceText
{
    /// <summary>Pads/truncates to exactly one 7-character scribble-strip cell, replacing non-printable characters.</summary>
    public static string Cell(string? s) => Fit(s, McuConstants.LcdCellWidth);

    /// <summary>Pads/truncates to <paramref name="width"/> printable ASCII characters.</summary>
    public static string Fit(string? s, int width)
    {
        var src = (s ?? "").PadRight(width)[..width];
        return string.Create(width, src, static (span, text) =>
        {
            for (int i = 0; i < text.Length; i++) span[i] = text[i] is >= ' ' and <= '~' ? text[i] : '?';
        });
    }

    /// <summary>Centers text within a cell.</summary>
    public static string Center(string? s)
    {
        s = (s ?? "").Trim();
        if (s.Length >= McuConstants.LcdCellWidth) return Cell(s);
        int left = (McuConstants.LcdCellWidth - s.Length) / 2;
        return Cell(new string(' ', left) + s);
    }
}
