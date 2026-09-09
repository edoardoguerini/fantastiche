using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Catalog;

public sealed record PublishCatalogCommand(RequestContext Context, Guid ListVersionId) : IRequest<ListVersionView>;
