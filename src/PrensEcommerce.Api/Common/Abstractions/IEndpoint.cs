namespace PrensEcommerce.Api.Common.Abstractions;

/// <summary>
/// Cada slice expone su propio endpoint. Se descubren por reflexión al arrancar,
/// así añadir una feature no obliga a tocar Program.cs (OCP).
/// </summary>
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
