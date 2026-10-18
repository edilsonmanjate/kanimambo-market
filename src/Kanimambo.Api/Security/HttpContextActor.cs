using Kanimambo.SharedKernel.Identity;

namespace Kanimambo.Api.Security;

internal sealed class HttpContextActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public const string Anonymous = "anonymous";

    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public string Name => IsAuthenticated ? accessor.HttpContext!.User.Identity!.Name ?? Anonymous : Anonymous;

    public bool IsInRole(string role) => accessor.HttpContext?.User.IsInRole(role) == true;
}
