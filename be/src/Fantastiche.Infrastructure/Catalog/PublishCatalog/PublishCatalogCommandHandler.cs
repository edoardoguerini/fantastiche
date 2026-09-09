using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class PublishCatalogCommandHandler(CatalogWorkflow workflow)
    : IRequestHandler<PublishCatalogCommand, ListVersionView>
{
    public Task<ListVersionView> HandleAsync(PublishCatalogCommand request, CancellationToken ct) => workflow.PublishAsync(request, ct);
}
