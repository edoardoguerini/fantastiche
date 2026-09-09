namespace Fantastiche.Application.Infrastructure.Http;

public sealed record ApiResponse<T>(
    bool IsSuccess,
    T? Data,
    IReadOnlyCollection<ApiError> Errors)
{
    public static ApiResponse<T> Success(T data) => new(true, data, []);

    public static ApiResponse<T> Failure(params ApiError[] errors) => new(false, default, errors);
}
