namespace Fantastiche.Application.Infrastructure.Http;

public static class ApiResults
{
    public static IResult Ok<T>(T data) => Results.Ok(ApiResponse<T>.Success(data));

    public static IResult Created<T>(string location, T data)
        => Results.Created(location, ApiResponse<T>.Success(data));

    public static IResult Error(int statusCode, string code, string message)
        => Results.Json(
            ApiResponse<object?>.Failure(new ApiError(code, message)),
            statusCode: statusCode);

    public static Task WriteErrorAsync(
        HttpContext context,
        int statusCode,
        string code,
        string message,
        CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(
            ApiResponse<object?>.Failure(new ApiError(code, message)),
            cancellationToken);
    }
}
