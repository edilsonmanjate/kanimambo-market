using System.Text;

namespace Kanimambo.Notifications.Infrastructure.Channels;

/// <summary>
/// SMS text rules: one SMS carries 160 characters of the GSM-7 alphabet. Portuguese letters outside it (ã, õ, ç, á, ê…)
/// would switch the whole message to UCS-2 and 70 characters, so they are written without the accent instead.
/// </summary>
public static class Gsm7Text
{
    public const int MaxLength = 160;
    private const string Basic = "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    private const string Extension = "^{}\\[~]|€";

    private static readonly Dictionary<char, string> Replacements = new()
    {
        ['á'] = "a", ['â'] = "a", ['ã'] = "a", ['ç'] = "c", ['ê'] = "e", ['í'] = "i", ['ó'] = "o", ['ô'] = "o", ['õ'] = "o", ['ú'] = "u",
        ['Á'] = "A", ['À'] = "A", ['Â'] = "A", ['Ã'] = "A", ['Ê'] = "E", ['Í'] = "I", ['Ó'] = "O", ['Ô'] = "O", ['Õ'] = "O", ['Ú'] = "U",
        ['—'] = "-", ['–'] = "-", ['“'] = "\"", ['”'] = "\"", ['‘'] = "'", ['’'] = "'", ['…'] = "...", [' '] = " ", ['º'] = "o", ['ª'] = "a",
    };

    /// <summary>Converts to GSM-7; anything it cannot represent becomes '?'.</summary>
    public static string Convert(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length);
        // By code point, not UTF-16 unit: an emoji is one character to the customer, so one '?'.
        foreach (var rune in text.EnumerateRunes())
        {
            if (!rune.IsBmp)
            {
                builder.Append('?');
                continue;
            }

            var c = (char)rune.Value;
            if (Basic.Contains(c, StringComparison.Ordinal) || Extension.Contains(c, StringComparison.Ordinal))
            {
                builder.Append(c);
            }
            else
            {
                builder.Append(Replacements.GetValueOrDefault(c, "?"));
            }
        }

        return builder.ToString();
    }

    /// <summary>Characters as the network counts them: extension characters (€, [, ]…) take two.</summary>
    public static int Length(string gsmText)
    {
        ArgumentNullException.ThrowIfNull(gsmText);
        return gsmText.Sum(c => Extension.Contains(c, StringComparison.Ordinal) ? 2 : 1);
    }

    /// <summary>Shortens at a word boundary, ending with "...", so the text fits one SMS.</summary>
    public static string Fit(string gsmText)
    {
        if (Length(gsmText) <= MaxLength)
        {
            return gsmText;
        }

        var cut = gsmText;
        while (Length(cut) > MaxLength - 3)
        {
            var space = cut.LastIndexOf(' ');
            cut = space > 0 ? cut[..space] : cut[..^1];
        }

        return cut.TrimEnd(' ', ',', '.', ':', ';') + "...";
    }
}
