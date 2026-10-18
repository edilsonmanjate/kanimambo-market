using Kanimambo.SharedKernel.Identity;
using Kanimambo.SharedKernel.Web.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Kanimambo.Api.Security;

internal static class SecurityExtensions
{
    /// <summary>
    /// JWT bearer authentication. Locally, tokens come from <c>dotnet user-jwts create --role backoffice</c>,
    /// which writes the signing key into user secrets (<c>Authentication:Schemes:Bearer</c>).
    /// </summary>
    public static IServiceCollection AddKanimamboSecurity(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.Backoffice, policy => policy.RequireRole(AuthorizationPolicies.BackofficeRole));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, HttpContextActor>();
        return services;
    }
}
