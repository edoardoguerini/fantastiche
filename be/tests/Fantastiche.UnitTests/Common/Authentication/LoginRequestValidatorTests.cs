using Fantastiche.Application.Modules.Auth;

namespace Fantastiche.UnitTests.Common.Authentication;

public sealed class LoginRequestValidatorTests
{
    [Theory]
    [InlineData("", "password", "validation.email")]
    [InlineData("non-email", "password", "validation.email")]
    [InlineData("user@example.test", "", "validation.password")]
    public void Validate_RifiutaPayloadNonValido(string email, string password, string expectedCode)
    {
        var result = new LoginRequestValidator().Validate(new LoginRequest(email, password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorCode == expectedCode);
    }
}
