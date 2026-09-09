using Fantastiche.Infrastructure.Common;
namespace Fantastiche.Infrastructure.Leagues;

public sealed class CreateLeagueCommandHandler(LeagueWorkflow workflow) : IRequestHandler<CreateLeagueCommand, LeagueDetails>
{
    public Task<LeagueDetails> HandleAsync(CreateLeagueCommand request, CancellationToken ct) => workflow.CreateLeagueAsync(request, ct);
}
