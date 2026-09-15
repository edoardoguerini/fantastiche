using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common;

namespace Fantastiche.Infrastructure.Auctions;

public sealed record ExportAuctionRosterQuery(RequestContext Context, Guid SessionId) : IRequest<AuctionRosterExportView>;

public sealed record AuctionRosterExportView(string FileName, string Csv);
