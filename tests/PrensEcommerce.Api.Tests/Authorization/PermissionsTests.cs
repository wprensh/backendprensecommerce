using PrensEcommerce.Api.Common.Authorization;
using PrensEcommerce.Api.Features.Roles;

namespace PrensEcommerce.Api.Tests.Authorization;

public sealed class PermissionsTests
{
    [Fact]
    public void Codes_are_unique()
    {
        var codes = Permissions.All.Select(p => p.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void Wildcard_expands_to_every_permission()
    {
        var expanded = Permissions.Expand([Permissions.Wildcard]);
        Assert.Equal(Permissions.All.Select(p => p.Code), expanded);
    }

    [Fact]
    public void Expand_merges_roles_and_drops_unknown_codes()
    {
        var expanded = Permissions.Expand([Permissions.CatalogRead, "legacy.code", Permissions.CatalogRead, Permissions.UsersRead]);
        Assert.Equal([Permissions.UsersRead, Permissions.CatalogRead], expanded);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("unknown.permission")]
    public void Role_validator_rejects_wildcard_and_unknown_codes(string code)
    {
        var result = new RoleCommandValidator().Validate(new RoleCommand("Ventas", null, [code]));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Role_validator_accepts_known_codes()
    {
        var result = new RoleCommandValidator().Validate(
            new RoleCommand("Ventas", "Equipo comercial", [Permissions.OrdersRead, Permissions.CustomersRead]));
        Assert.True(result.IsValid);
    }
}
