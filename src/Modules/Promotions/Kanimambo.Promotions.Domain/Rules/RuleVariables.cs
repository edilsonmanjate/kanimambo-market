namespace Kanimambo.Promotions.Domain.Rules;

/// <summary>
/// The vocabulary marketing can use in rules. Rules are type-checked against it when they are saved, so a
/// typo or a wrong comparison is caught at authoring time, never at checkout.
/// </summary>
public static class RuleVariables
{
    public const string CartTotal = "cart.total";
    public const string CartItemCount = "cart.itemCount";
    public const string CartCategories = "cart.categories";
    public const string CustomerCity = "customer.city";
    public const string CustomerType = "customer.type";
    public const string PaymentMethod = "payment.method";
    public const string TodayWeekday = "today.weekday";

    private static readonly Dictionary<string, RuleVariable> ByName = new RuleVariable[]
    {
        new(CartTotal, RuleType.Number, "Cart subtotal in MZN, before promotions."),
        new(CartItemCount, RuleType.Number, "Number of units in the cart."),
        new(CartCategories, RuleType.TextList, "Categories of the products in the cart, e.g. \"cabazes\" IN cart.categories."),
        new(CustomerCity, RuleType.Text, "Delivery city, e.g. \"Maputo\", \"Matola\", \"Beira\", \"Nampula\"."),
        new(CustomerType, RuleType.Text, "\"guest\", \"registered\" or \"b2b\"."),
        new(PaymentMethod, RuleType.Text, "\"MPesa\", \"EMola\", \"MKesh\", \"Card\", \"CashOnDelivery\" or \"None\" (not chosen yet)."),
        new(TodayWeekday, RuleType.Text, "Day of the week in Maputo time, e.g. \"Friday\"."),
    }.ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<RuleVariable> All => ByName.Values;

    public static RuleVariable? Find(string name) => ByName.GetValueOrDefault(name);

    /// <summary>Closest known name within two edits, to suggest a fix for typos like <c>custmer.city</c>.</summary>
    public static string? Suggest(string name) =>
        ByName.Keys
            .Select(known => (Known: known, Distance: Levenshtein(name.ToLowerInvariant(), known.ToLowerInvariant())))
            .Where(c => c.Distance <= 2)
            .OrderBy(c => c.Distance)
            .Select(c => c.Known)
            .FirstOrDefault();

    private static int Levenshtein(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            previous = current;
        }

        return previous[b.Length];
    }
}
