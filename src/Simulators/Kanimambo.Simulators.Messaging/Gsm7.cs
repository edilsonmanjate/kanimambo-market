namespace Kanimambo.Simulators.Messaging;

/// <summary>The GSM 03.38 default alphabet (and its extension table, two septets each): what an SMS can carry as 160 characters.</summary>
public static class Gsm7
{
    public const string Basic = "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";
    public const string Extension = "^{}\\[~]|€\f";

    /// <summary>Length in septets, or null when the text has characters outside GSM-7.</summary>
    public static int? Septets(string text)
    {
        var septets = 0;
        foreach (var c in text)
        {
            if (Basic.Contains(c, StringComparison.Ordinal))
            {
                septets++;
            }
            else if (Extension.Contains(c, StringComparison.Ordinal))
            {
                septets += 2;
            }
            else
            {
                return null;
            }
        }

        return septets;
    }
}
