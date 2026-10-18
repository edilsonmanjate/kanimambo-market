using System.Globalization;

namespace Kanimambo.Notifications.Application.Messages;

/// <summary>Amounts as Mozambicans read them: <c>4 583,10 MZN</c>.</summary>
public static class Mzn
{
    private static readonly NumberFormatInfo Format = new() { NumberGroupSeparator = " ", NumberDecimalSeparator = ",", NumberGroupSizes = [3] };

    public static string Of(decimal amount) => $"{amount.ToString("#,##0.00", Format)} MZN";
}
