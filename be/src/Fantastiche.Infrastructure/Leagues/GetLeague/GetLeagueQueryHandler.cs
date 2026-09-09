using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed class GetLeagueQueryHandler(LeagueWorkflow workflow) : IRequestHandler<GetLeagueQuery, LeagueDetails>
{
    public Task<LeagueDetails> HandleAsync(GetLeagueQuery request, CancellationToken ct) => workflow.GetLeagueAsync(request, ct);
}
