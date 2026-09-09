using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed class ResendInvitationCommandHandler(LeagueWorkflow workflow) : IRequestHandler<ResendInvitationCommand, InvitationDetails>
{
    public Task<InvitationDetails> HandleAsync(ResendInvitationCommand request, CancellationToken ct) => workflow.ResendInvitationAsync(request, ct);
}
