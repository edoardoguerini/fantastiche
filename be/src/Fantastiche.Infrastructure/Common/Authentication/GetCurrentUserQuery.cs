namespace Fantastiche.Infrastructure.Common.Authentication;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<AuthenticatedUser>;
