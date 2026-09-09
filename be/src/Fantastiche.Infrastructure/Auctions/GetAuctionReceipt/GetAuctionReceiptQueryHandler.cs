using System.Text.Json;
using Dapper;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;

namespace Fantastiche.Infrastructure.Auctions;

public sealed class GetAuctionReceiptQueryHandler(FantasticheDbContext db)
    : IRequestHandler<GetAuctionReceiptQuery, AuctionCommandResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuctionCommandResult> HandleAsync(GetAuctionReceiptQuery request, CancellationToken ct)
    {
        await using var read = await AuctionReadSession.OpenAsync(db, request.Context, request.SessionId, ct);
        var json = await read.Connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
            SELECT ResultJson
            FROM CommandReceipts
            WHERE SessionId = @sessionId AND UserId = @userId AND RequestId = @requestId;
            """, new
        {
            sessionId = request.SessionId,
            userId = request.Context.UserId!.Value,
            requestId = request.RequestId
        }, read.Transaction, cancellationToken: ct));
        if (json is null) throw AuctionReadSession.NotFound();
        var result = JsonSerializer.Deserialize<AuctionCommandResult>(json, JsonOptions)
            ?? throw new JsonException("La ricevuta d'asta non contiene un risultato valido.");
        await read.CommitAsync(ct);
        return result;
    }
}
