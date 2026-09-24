namespace ReciclaApi.Application.Common;

public sealed record ServiceResult<T>(bool Succeeded, T? Value, int StatusCode, string? Error)
{
    public static ServiceResult<T> Ok(T value, int statusCode = StatusCodes.Status200OK) =>
        new(true, value, statusCode, null);

    public static ServiceResult<T> Fail(string error, int statusCode) =>
        new(false, default, statusCode, error);
}

public sealed record ServiceResult(bool Succeeded, int StatusCode, string? Error)
{
    public static ServiceResult Ok(int statusCode = StatusCodes.Status204NoContent) =>
        new(true, statusCode, null);

    public static ServiceResult Fail(string error, int statusCode) =>
        new(false, statusCode, error);
}
