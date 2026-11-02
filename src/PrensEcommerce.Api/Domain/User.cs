namespace PrensEcommerce.Api.Domain;

/// <summary>Cliente de una tienda. El email es único por tenant, no global.</summary>
public sealed class User : TenantEntity
{
    public required string Name { get; set; }

    public required string Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;
}
