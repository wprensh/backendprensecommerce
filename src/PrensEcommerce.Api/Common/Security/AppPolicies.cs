using Microsoft.AspNetCore.Authorization;
using PrensEcommerce.Api.Domain;

namespace PrensEcommerce.Api.Common.Security;

public static class AppPolicies
{
    /// <summary>
    /// Agentes de atención al cliente: rol <c>Admin</c> o <c>CustomerService</c>, o un claim
    /// <c>perm</c> con <c>support.write</c> (cuando los permisos viajen en el token).
    /// </summary>
    public const string SupportAgent = "support-agent";

    /// <summary>Gestión del programa de puntos: rol <c>Admin</c> o <c>Marketing</c>, o permiso <c>crm.write</c>.</summary>
    public const string LoyaltyManager = "loyalty-manager";

    public static AuthorizationBuilder AddAppPolicies(this AuthorizationBuilder builder) =>
        builder.AddPolicy(SupportAgent, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx =>
                ctx.User.HasClaim(AppClaims.Role, SystemRoles.Admin) ||
                ctx.User.HasClaim(AppClaims.Role, SystemRoles.CustomerService) ||
                ctx.User.HasClaim("perm", "support.write") ||
                ctx.User.HasClaim("perm", "*")))
        .AddPolicy(LoyaltyManager, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(ctx =>
                ctx.User.HasClaim(AppClaims.Role, SystemRoles.Admin) ||
                ctx.User.HasClaim(AppClaims.Role, SystemRoles.Marketing) ||
                ctx.User.HasClaim("perm", "crm.write") ||
                ctx.User.HasClaim("perm", "*")));
}
