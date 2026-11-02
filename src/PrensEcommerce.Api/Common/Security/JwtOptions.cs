using System.ComponentModel.DataAnnotations;

namespace PrensEcommerce.Api.Common.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "prens-ecommerce";

    [Required]
    public string Audience { get; set; } = "prens-ecommerce-web";

    /// <summary>Clave HMAC de al menos 32 caracteres. En producción, por variable de entorno <c>Jwt__SigningKey</c>.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(5, 1440)]
    public int ExpirationMinutes { get; set; } = 60;
}
