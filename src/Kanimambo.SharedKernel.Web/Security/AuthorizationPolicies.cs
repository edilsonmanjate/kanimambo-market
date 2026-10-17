namespace Kanimambo.SharedKernel.Web.Security;

public static class AuthorizationPolicies
{
    /// <summary>Back-office staff: audited, undoable operations on sellers and prices.</summary>
    public const string Backoffice = "backoffice";

    public const string BackofficeRole = "backoffice";
}
