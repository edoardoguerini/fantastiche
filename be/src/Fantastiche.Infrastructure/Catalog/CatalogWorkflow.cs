using System.Data;
using System.Security.Cryptography;
using System.Text;
using Fantastiche.Core.Auth;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Leagues;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fantastiche.Infrastructure.Catalog;

public sealed class CatalogWorkflow(FantasticheDbContext db, TimeProvider clock)
{
    public const string FantacalcioCsvSource = "FantacalcioCsv";

    public async Task<ListVersionView> ImportAsync(ImportCatalogCommand request, CancellationToken ct)
    {
        await RequireAdminAsync(request.Context, ct);
        var seasonName = Required(request.SeasonName, 50);
        var rows = FantacalcioCsvParser.Parse(request.Csv);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Csv)));

        await using var tx = await BeginCatalogTransactionAsync(ct);
        await RequireAdminAsync(request.Context, ct);

        var existing = await db.ListVersions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Source == FantacalcioCsvSource && x.SeasonName == seasonName && x.ContentHash == hash, ct);
        if (existing is not null)
        {
            await tx.CommitAsync(ct);
            return View(existing);
        }

        var players = (await db.Players.Where(x => x.Source == FantacalcioCsvSource).ToListAsync(ct))
            .ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
        var clubs = (await db.Clubs.Where(x => x.Source == FantacalcioCsvSource).ToListAsync(ct))
            .ToDictionary(x => x.NormalizedName, StringComparer.Ordinal);
        var version = new ListVersion
        {
            SeasonName = seasonName,
            Source = FantacalcioCsvSource,
            ContentHash = hash,
            EntryCount = rows.Count,
            CreatedByUserId = request.Context.UserId!.Value,
            CreatedAt = clock.GetUtcNow()
        };
        db.ListVersions.Add(version);

        foreach (var row in rows)
        {
            if (!players.TryGetValue(row.ExternalId, out var player))
            {
                player = new Player { Source = FantacalcioCsvSource, ExternalId = row.ExternalId };
                players.Add(player.ExternalId, player);
                db.Players.Add(player);
            }

            var normalizedClubName = NormalizeClubName(row.ClubName);
            if (!clubs.TryGetValue(normalizedClubName, out var club))
            {
                club = new Club
                {
                    Source = FantacalcioCsvSource,
                    Name = row.ClubName,
                    NormalizedName = normalizedClubName
                };
                clubs.Add(normalizedClubName, club);
                db.Clubs.Add(club);
            }

            db.ListEntries.Add(new ListEntry
            {
                ListVersionId = version.Id,
                PlayerId = player.Id,
                ClubId = club.Id,
                Name = row.Name,
                FullName = row.FullName,
                Role = row.Role,
                ClubName = row.ClubName,
                BirthDate = row.BirthDate,
                Nationality = row.Nationality,
                PreferredFoot = row.PreferredFoot
            });
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return View(version);
    }

    public async Task<ListVersionView> PublishAsync(PublishCatalogCommand request, CancellationToken ct)
    {
        await using var tx = await BeginCatalogTransactionAsync(ct);
        await RequireAdminAsync(request.Context, ct);
        var version = await db.ListVersions.SingleOrDefaultAsync(x => x.Id == request.ListVersionId, ct) ?? throw NotFound();
        if (version.Status == ListVersionStatus.Draft)
        {
            version.Status = ListVersionStatus.Published;
            version.PublishedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return View(version);
    }

    public async Task<LeagueCatalogView> SetLeagueCatalogAsync(SetLeagueCatalogCommand request, CancellationToken ct)
    {
        await using var tx = await BeginCatalogTransactionAsync(ct);
        if (request.Context.UserId is null) throw Unauthorized();

        var season = await db.LeagueSeasons.SingleOrDefaultAsync(
            x => x.Id == request.LeagueSeasonId && x.LeagueId == request.LeagueId, ct) ?? throw NotFound();
        if (!await CatalogAuthorization.IsSuperAdminAsync(db, request.Context, ct))
        {
            var organizer = await db.LeagueMembers.AsNoTracking().AnyAsync(x =>
                x.LeagueId == request.LeagueId &&
                x.UserId == request.Context.UserId.Value &&
                x.Status == MembershipStatus.Active &&
                x.IsOrganizer, ct);
            if (!organizer) throw Forbidden();
        }

        var version = await db.ListVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ListVersionId, ct) ?? throw NotFound();
        if (version.Status != ListVersionStatus.Published)
            throw new DomainException("catalog.not_published", "La versione del listone non è pubblicata.", 409);
        if (!await db.ListVersions.AsNoTracking().AnyAsync(
                x => x.Id == version.Id && x.SeasonName == season.Name, ct))
            throw new DomainException("catalog.season_mismatch", "La versione del listone appartiene a una stagione diversa.", 409);
        if (season.ListVersionId is not null && season.ListVersionId != version.Id)
            throw new DomainException("catalog.already_selected", "La stagione usa già un’altra versione del listone.", 409);

        if (season.ListVersionId is null)
        {
            season.ListVersionId = version.Id;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new LeagueCatalogView(request.LeagueId, request.LeagueSeasonId, version.Id);
    }

    internal static ListVersionView View(ListVersion version) => new(
        version.Id,
        version.SeasonName,
        version.Status.ToString(),
        version.Source,
        version.ContentHash,
        version.EntryCount,
        version.CreatedAt,
        version.PublishedAt);

    private async Task<IDbContextTransaction> BeginCatalogTransactionAsync(CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource = N'Fantastiche:Catalog', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @r < 0 THROW 51000, 'Catalog busy', 1;",
                ct);
            return tx;
        }
        catch
        {
            await tx.DisposeAsync();
            throw;
        }
    }

    private static string Required(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
            throw new DomainException("catalog.invalid_season", "La stagione è obbligatoria e non può superare 50 caratteri.");
        return value.Trim();
    }

    internal static string NormalizeClubName(string value) => value.Trim().ToUpperInvariant();

    private async Task RequireAdminAsync(RequestContext context, CancellationToken ct)
    {
        if (!await CatalogAuthorization.IsSuperAdminAsync(db, context, ct)) throw Forbidden();
    }

    private static DomainException Unauthorized() => new("auth.required", "Accesso richiesto.", 401);
    private static DomainException Forbidden() => new("auth.forbidden", "Operazione non consentita.", 403);
    private static DomainException NotFound() => new("resource.not_found", "Risorsa non disponibile.", 404);
}
