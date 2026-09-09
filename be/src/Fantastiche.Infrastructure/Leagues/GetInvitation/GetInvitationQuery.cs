using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed record GetInvitationQuery(RequestContext Context, string Token) : IRequest<InvitationPreview>;
