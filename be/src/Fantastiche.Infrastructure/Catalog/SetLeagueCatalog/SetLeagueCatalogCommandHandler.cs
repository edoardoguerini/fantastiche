using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class SetLeagueCatalogCommandHandler(CatalogWorkflow workflow)
    : IRequestHandler<SetLeagueCatalogCommand, LeagueCatalogView>
{
    public Task<LeagueCatalogView> HandleAsync(SetLeagueCatalogCommand request, CancellationToken ct) =>
        workflow.SetLeagueCatalogAsync(request, ct);
}
