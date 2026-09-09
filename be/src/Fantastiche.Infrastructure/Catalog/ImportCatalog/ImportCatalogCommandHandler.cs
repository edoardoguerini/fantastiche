using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class ImportCatalogCommandHandler(CatalogWorkflow workflow)
    : IRequestHandler<ImportCatalogCommand, ListVersionView>
{
    public Task<ListVersionView> HandleAsync(ImportCatalogCommand request, CancellationToken ct) => workflow.ImportAsync(request, ct);
}
