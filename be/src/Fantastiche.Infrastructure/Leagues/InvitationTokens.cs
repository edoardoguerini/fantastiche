using System.Security.Cryptography;
using System.Text;
using Fantastiche.Core.Exceptions;
namespace Fantastiche.Infrastructure.Leagues;

public static class InvitationTokens
{
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 64 || !token.All(Uri.IsHexDigit))
            throw new DomainException("invitation.invalid", "Invito non valido.", 404);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
