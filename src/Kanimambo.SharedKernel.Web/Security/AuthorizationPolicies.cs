using Kanimambo.SharedKernel.Identity;

namespace Kanimambo.SharedKernel.Web.Security;

public static class AuthorizationPolicies
{
    /// <summary>Back-office staff: audited, undoable operations on sellers, prices and promotions.</summary>
    public const string Backoffice = "backoffice";

    public const string BackofficeRole = KnownRoles.Backoffice;

    public const string B2BRole = KnownRoles.B2B;
}
