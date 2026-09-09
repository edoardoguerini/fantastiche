using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed class InviteMemberCommandHandler(LeagueWorkflow workflow) : IRequestHandler<InviteMemberCommand, InvitationDetails>
{
    public Task<InvitationDetails> HandleAsync(InviteMemberCommand request, CancellationToken ct) => workflow.InviteMemberAsync(request, ct);
}
