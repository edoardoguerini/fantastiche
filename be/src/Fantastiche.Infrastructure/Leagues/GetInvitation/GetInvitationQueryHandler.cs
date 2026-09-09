using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed class GetInvitationQueryHandler(LeagueWorkflow workflow) : IRequestHandler<GetInvitationQuery, InvitationPreview>
{
    public Task<InvitationPreview> HandleAsync(GetInvitationQuery request, CancellationToken ct) => workflow.GetInvitationAsync(request, ct);
}
