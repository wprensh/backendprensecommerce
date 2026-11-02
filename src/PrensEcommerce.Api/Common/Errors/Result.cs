namespace PrensEcommerce.Api.Common.Errors;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
}

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
}

/// <summary>Resultado de un caso de uso: valor o error de negocio, sin excepciones para el flujo esperado.</summary>
public sealed class Result<T>
{
    private Result(T? value, Error? error) => (Value, Error) = (value, error);

    public T? Value { get; }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}

public static class ResultExtensions
{
    /// <summary>Traduce un <see cref="Error"/> de negocio a ProblemDetails (RFC 9457).</summary>
    public static IResult ToProblem(this Error error) => Results.Problem(
        statusCode: error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError,
        },
        title: error.Code,
        detail: error.Message);

    public static IResult Match<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value!) : result.Error!.ToProblem();
}
