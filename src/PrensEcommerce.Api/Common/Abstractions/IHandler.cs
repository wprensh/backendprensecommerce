namespace PrensEcommerce.Api.Common.Abstractions;

/// <summary>
/// Marca el handler (caso de uso) de un slice. Se registran como Scoped automáticamente.
/// Sin MediatR: el endpoint inyecta su handler concreto, sin indirecciones.
/// </summary>
public interface IHandler;
