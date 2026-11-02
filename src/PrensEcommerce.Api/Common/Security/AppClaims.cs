namespace PrensEcommerce.Api.Common.Security;

public static class AppClaims
{
    /// <summary>Claim con el Id del tenant para el que se emitió el token.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>Claim de rol (uno por rol) usado por RequireRole.</summary>
    public const string Role = "role";
}
