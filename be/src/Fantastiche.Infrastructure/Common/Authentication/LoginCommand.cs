namespace Fantastiche.Infrastructure.Common.Authentication;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthenticatedUser>;
