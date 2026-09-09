namespace Fantastiche.Core.Exceptions;

public sealed class DomainException : Exception
{
    public DomainException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }
}
