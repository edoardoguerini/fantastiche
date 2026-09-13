using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed record AcceptInvitationCommand(RequestContext Context, string Token, string? Password, string? TeamName, string? DisplayName = null) : IRequest<AcceptanceDetails>;
