namespace Fantastiche.Core.Auth;

public sealed record RequestContext(
    Guid? UserId,
    bool IsSuperAdmin,
    string CorrelationId);
