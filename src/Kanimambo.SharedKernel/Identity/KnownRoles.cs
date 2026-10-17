namespace Kanimambo.SharedKernel.Identity;

/// <summary>Role names carried in access tokens. Kept here so application code can use them without HTTP types.</summary>
public static class KnownRoles
{
    /// <summary>Back-office staff: audited, undoable operations on sellers, prices and promotions.</summary>
    public const string Backoffice = "backoffice";

    /// <summary>Business customers (restaurants, grocers) buying wholesale.</summary>
    public const string B2B = "b2b";
}
