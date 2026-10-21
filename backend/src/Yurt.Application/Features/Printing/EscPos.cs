using System.Text;

namespace Yurt.Application.Features.Printing;

/// <summary>
/// Builds the bytes for an ESC/POS thermal printer (the command set nearly every receipt printer understands). Text is
/// sent in code page 866, which holds Russian; Kazakh letters it does not hold are printed as the closest Russian one.
/// </summary>
public sealed class EscPos
{
    private const byte Esc = 0x1B, Gs = 0x1D, Lf = 0x0A;

    private static readonly Encoding Cp866;
    private static readonly Dictionary<char, string> Replacements = new()
    {
        ['ә'] = "а", ['Ә'] = "А", ['ғ'] = "г", ['Ғ'] = "Г", ['қ'] = "к", ['Қ'] = "К", ['ң'] = "н", ['Ң'] = "Н",
        ['ө'] = "о", ['Ө'] = "О", ['ұ'] = "у", ['Ұ'] = "У", ['ү'] = "у", ['Ү'] = "У", ['һ'] = "х", ['Һ'] = "Х",
        ['і'] = "i", ['І'] = "I", ['₸'] = "тг", ['×'] = "x", ['−'] = "-", ['–'] = "-", ['—'] = "-", ['«'] = "\"",
        ['»'] = "\"", ['“'] = "\"", ['”'] = "\"", ['’'] = "'", ['…'] = "...", ['№'] = "N",
    };

    static EscPos()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp866 = Encoding.GetEncoding(866, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
    }

    private readonly List<byte> _bytes = [];

    public int Width { get; }

    /// <param name="width">Characters per line (32 for 58 mm paper, 48 for 80 mm).</param>
    /// <param name="codeTable">The printer's character table number for code page 866.</param>
    public EscPos(int width, int codeTable)
    {
        Width = width;
        Raw(Esc, (byte)'@');                // reset
        Raw(Esc, (byte)'t', (byte)codeTable); // character table
    }

    public EscPos Raw(params byte[] bytes)
    {
        _bytes.AddRange(bytes);
        return this;
    }

    public EscPos Center() => Raw(Esc, (byte)'a', 1);
    public EscPos Left() => Raw(Esc, (byte)'a', 0);
    public EscPos Bold(bool on) => Raw(Esc, (byte)'E', (byte)(on ? 1 : 0));

    /// <summary>Double width and height. Each line then holds half as many characters.</summary>
    public EscPos Big(bool on) => Raw(Gs, (byte)'!', (byte)(on ? 0x11 : 0x00));

    /// <summary>Prints text and ends the line. Long text wraps at <paramref name="width"/> (the full width by default).</summary>
    public EscPos Line(string text = "", int? width = null)
    {
        foreach (var line in Wrap(Clean(text), width ?? Width)) Text(line).Raw(Lf);
        return this;
    }

    /// <summary>Text on the left and a value on the right of one line; a long left part wraps above the value.</summary>
    public EscPos Columns(string left, string right)
    {
        left = Clean(left);
        right = Clean(right);
        var lines = Wrap(left, Math.Max(1, Width - right.Length - 1));
        for (var i = 0; i < lines.Count - 1; i++) Text(lines[i]).Raw(Lf);
        var last = lines[^1];
        return Text(last + new string(' ', Math.Max(1, Width - last.Length - right.Length)) + right).Raw(Lf);
    }

    public EscPos Rule(char c = '-') => Text(new string(c, Width)).Raw(Lf);

    /// <summary>Feeds a little paper and cuts it (a printer with no cutter just feeds).</summary>
    public EscPos Cut() => Raw(Esc, (byte)'d', 4).Raw(Gs, (byte)'V', 66, 0);

    /// <summary>
    /// Opens the cash drawer plugged into the printer (a short pulse on the drawer socket). Drawers are wired to pin 2 or
    /// pin 5, so both get the pulse; only the one with a drawer does anything.
    /// </summary>
    public EscPos OpenDrawer() => Raw(Esc, (byte)'p', 0, 25, 250).Raw(Esc, (byte)'p', 1, 25, 250);

    public byte[] ToArray() => [.. _bytes];

    private EscPos Text(string text)
    {
        _bytes.AddRange(Cp866.GetBytes(text));
        return this;
    }

    /// <summary>Swaps characters code page 866 does not have for close ones and drops control characters.</summary>
    public static string Clean(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (Replacements.TryGetValue(c, out var r)) sb.Append(r);
            else if (c == '\n' || c == '\r' || c == '\t') sb.Append(' ');
            else if (!char.IsControl(c)) sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    public static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = word;
            while (w.Length > width)
            {
                if (current.Length > 0) { lines.Add(current.ToString()); current.Clear(); }
                lines.Add(w[..width]);
                w = w[width..];
            }
            if (current.Length > 0 && current.Length + 1 + w.Length > width) { lines.Add(current.ToString()); current.Clear(); }
            if (current.Length > 0) current.Append(' ');
            current.Append(w);
        }
        if (current.Length > 0 || lines.Count == 0) lines.Add(current.ToString());
        return lines;
    }
}
