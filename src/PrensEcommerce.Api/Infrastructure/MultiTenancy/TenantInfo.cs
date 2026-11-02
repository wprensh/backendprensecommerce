namespace PrensEcommerce.Api.Infrastructure.MultiTenancy;

/// <summary>Instantánea inmutable (y cacheable) de un tenant activo.</summary>
public sealed record TenantInfo(Guid Id, string Slug, string Name);
