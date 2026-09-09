namespace Fantastiche.Infrastructure.Common.Authentication;

public sealed record AuthenticatedUser(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsSuperAdmin);
