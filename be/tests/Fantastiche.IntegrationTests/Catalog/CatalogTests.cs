using Fantastiche.Core.Auth;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Leagues;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.IntegrationTests.Catalog;

public sealed class CatalogTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    private static readonly RequestContext Anonymous = new(null, false, "integration");
    private static int nextExternalId = 1_000_000;

    [Fact]
    public async Task ReimportCompletesMissingMarketDataWithoutReplacingPublishedVersion()
    {
        var csv = Csv(Row(UniqueExternalId(), "Portiere", "Portiere Uno", "P", "Roma"));
        var season = UniqueSeason();
        var version = await Send<ImportCatalogCommand, ListVersionView>(new(fixture.Admin, season, csv));
        var published = await Send<PublishCatalogCommand, ListVersionView>(new(fixture.Admin, version.Id));
        var playerId = await Run(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            var entry = await db.ListEntries.SingleAsync(x => x.ListVersionId == version.Id);
            Assert.Equal(17, entry.CurrentQuotation);
            Assert.Equal(57, entry.Fvm);
            entry.CurrentQuotation = null;
            entry.Fvm = null;
            entry.IsTransferred = null;
            await db.SaveChangesAsync();
            return entry.PlayerId;
        });
        var replay = await Send<ImportCatalogCommand, ListVersionView>(new(fixture.Admin, season, csv));
        Assert.Equal(published, replay);
        await Run(async services =>
        {
            var entry = await services.GetRequiredService<FantasticheDbContext>().ListEntries
                .SingleAsync(x => x.ListVersionId == version.Id);
            Assert.Equal(playerId, entry.PlayerId);
            Assert.Equal(17, entry.CurrentQuotation);
            Assert.Equal(57, entry.Fvm);
            Assert.False(entry.IsTransferred);
        });
        Assert.Equal(replay, await Send<ImportCatalogCommand, ListVersionView>(new(fixture.Admin, season, csv)));
    }

    [Fact]
    public async Task ImportRequiresAnAuthenticatedSuperAdminAndConcurrentReplayReturnsOneDraft()
    {
        var externalId = UniqueExternalId();
        var csv = Csv(Row(externalId, "Portiere", "Portiere Uno", "P", "Roma"));

        var unauthorized = await Assert.ThrowsAsync<DomainException>(() =>
            Send<ImportCatalogCommand, ListVersionView>(new(Anonymous, "2026/27", csv)));
        Assert.Equal(403, unauthorized.StatusCode);
        var regularUser = await CreateUser();
        Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() =>
            Send<ImportCatalogCommand, ListVersionView>(new(regularUser, "2026/27", csv)))).StatusCode);

        async Task<ListVersionView> Import() => await Send<ImportCatalogCommand, ListVersionView>(
            new(fixture.Admin, " 2026/27 ", csv));

        var versions = await Task.WhenAll(Import(), Import());

        Assert.Equal(versions[0].Id, versions[1].Id);
        Assert.Equal("Draft", versions[0].Status);
        Assert.Equal("FantacalcioCsv", versions[0].Source);
        Assert.Equal("2026/27", versions[0].SeasonName);
        Assert.Equal(64, versions[0].ContentHash.Length);
        Assert.Equal(1, versions[0].EntryCount);
        Assert.Null(versions[0].PublishedAt);

        await Run(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            Assert.Single(await db.ListVersions.Where(x => x.SeasonName == "2026/27" && x.ContentHash == versions[0].ContentHash).ToListAsync());
            Assert.Single(await db.Players.Where(x => x.ExternalId == externalId).ToListAsync());
            Assert.Single(await db.ListEntries.Where(x => x.ListVersionId == versions[0].Id).ToListAsync());
        });
    }

    [Fact]
    public async Task InvalidCsvDoesNotPersistAPartialVersion()
    {
        var season = UniqueSeason();
        var csv = Csv(Row(UniqueExternalId(), "Valido", "Valido", "P", "Roma"), "riga,non,valida");

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            Send<ImportCatalogCommand, ListVersionView>(new(fixture.Admin, season, csv)));

        Assert.Equal("catalog.invalid_csv", error.Code);
        await Run(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            Assert.False(await db.ListVersions.AnyAsync(x => x.SeasonName == season));
        });
    }

    [Fact]
    public async Task CatalogLockTimesOutAfterTenSecondsWithTheMappedConflictNumber()
    {
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqlTransaction)transaction;
            command.CommandText = "DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource = N'Fantastiche:Catalog', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 0; IF @r < 0 THROW 51000, 'Test lock failed', 1;";
            await command.ExecuteNonQueryAsync();
        }

        var error = await Assert.ThrowsAsync<SqlException>(() =>
            Send<ImportCatalogCommand, ListVersionView>(new(
                fixture.Admin,
                UniqueSeason(),
                Csv(Row(UniqueExternalId(), "Lock", "Lock", "P", "Roma")))));

        Assert.Equal(51000, error.Number);
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task DraftIsAdminOnlyAndPublishingMakesItVisibleWithoutChangingTheInitialTimestamp()
    {
        var user = await CreateUser();
        var imported = await Send<ImportCatalogCommand, ListVersionView>(new(
            fixture.Admin,
            UniqueSeason(),
            Csv(Row(UniqueExternalId(), "Ala", "Ala Pubblicata", "A", "Napoli"))));

        var hidden = await Query<GetCatalogVersionsQuery, CatalogPage<ListVersionView>>(new(user, imported.SeasonName));
        Assert.Empty(hidden.Items);
        var forbidden = await Assert.ThrowsAsync<DomainException>(() =>
            Query<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(new(user, imported.Id)));
        Assert.Equal(404, forbidden.StatusCode);
        Assert.Equal(401, (await Assert.ThrowsAsync<DomainException>(() =>
            Query<GetCatalogVersionsQuery, CatalogPage<ListVersionView>>(new(Anonymous)))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() =>
            Send<PublishCatalogCommand, ListVersionView>(new(user, imported.Id)))).StatusCode);

        var published = await Send<PublishCatalogCommand, ListVersionView>(new(fixture.Admin, imported.Id));
        var replay = await Send<PublishCatalogCommand, ListVersionView>(new(fixture.Admin, imported.Id));

        Assert.Equal("Published", published.Status);
        Assert.NotNull(published.PublishedAt);
        Assert.Equal(published.PublishedAt, replay.PublishedAt);
        var visible = await Query<GetCatalogVersionsQuery, CatalogPage<ListVersionView>>(new(user, imported.SeasonName));
        Assert.Equal(imported.Id, Assert.Single(visible.Items).Id);
        Assert.Equal("Published", visible.Items[0].Status);
        Assert.Single((await Query<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(new(user, imported.Id))).Items);
    }

    [Fact]
    public async Task ReimportAfterTransferKeepsPlayerIdentityAndOldSnapshotImmutable()
    {
        var externalId = UniqueExternalId();
        var season = UniqueSeason();
        var first = await Send<ImportCatalogCommand, ListVersionView>(new(
            fixture.Admin,
            season,
            Csv(Row(externalId, "Prima", "Nome Prima", "C", "Roma"))));
        var second = await Send<ImportCatalogCommand, ListVersionView>(new(
            fixture.Admin,
            season,
            Csv(Row(externalId, "Dopo", "Nome Dopo", "C", "Milan"))));

        var oldEntry = Assert.Single((await Query<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(
            new(fixture.Admin, first.Id))).Items);
        var newEntry = Assert.Single((await Query<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(
            new(fixture.Admin, second.Id))).Items);

        Assert.Equal(oldEntry.PlayerId, newEntry.PlayerId);
        Assert.Equal("Prima", oldEntry.Name);
        Assert.Equal("Roma", oldEntry.ClubName);
        Assert.Equal("Dopo", newEntry.Name);
        Assert.Equal("Milan", newEntry.ClubName);
        await Run(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            Assert.Single(await db.Players.Where(x => x.ExternalId == externalId).ToListAsync());
            Assert.Equal(2, await db.Clubs.Where(x => x.Name == "Roma" || x.Name == "Milan").CountAsync());
        });
    }

    [Fact]
    public async Task EntryQueriesApplyParameterizedFiltersPaginationAndLiteralLikeEscaping()
    {
        var season = UniqueSeason();
        var version = await Send<ImportCatalogCommand, ListVersionView>(new(
            fixture.Admin,
            season,
            Csv(
                Row(UniqueExternalId(), "Uno%", "Uno Percentuale", "P", "Roma"),
                Row(UniqueExternalId(), "Due_", "Due Trattino", "D", "Roma"),
                Row(UniqueExternalId(), "Tre[", "Tre Parentesi", "D", "Milan"))));

        var literalPercent = await Query<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(new(
            fixture.Admin, version.Id, "%"));
        Assert.Equal("Uno%", Assert.Single(literalPercent.Items).Name);
        Assert.Equal("Due_", Assert.Single((await Query<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(new(
            fixture.Admin, version.Id, "_"))).Items).Name);
        Assert.Equal("Tre[", Assert.Single((await Query<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(new(
            fixture.Admin, version.Id, "["))).Items).Name);

        var filtered = await Query<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(new(
            fixture.Admin, version.Id, null, "D", "Roma", 1, 1));
        Assert.Equal(1, filtered.Total);
        Assert.Equal("Due_", Assert.Single(filtered.Items).Name);

        foreach (var query in new[]
        {
            new GetCatalogEntriesQuery(fixture.Admin, version.Id, Page: 0),
            new GetCatalogEntriesQuery(fixture.Admin, version.Id, Page: 10001),
            new GetCatalogEntriesQuery(fixture.Admin, version.Id, PageSize: 101),
            new GetCatalogEntriesQuery(fixture.Admin, version.Id, new string('x', 101)),
            new GetCatalogEntriesQuery(fixture.Admin, version.Id, Role: "X")
        })
        {
            var error = await Assert.ThrowsAsync<DomainException>(() =>
                Query<GetCatalogEntriesQuery, CatalogPage<CatalogEntryView>>(query));
            Assert.Equal("catalog.invalid_query", error.Code);
        }
    }

    [Fact]
    public async Task LeagueSelectionRequiresScopeOrganizerPublishedMatchingSeasonAndCannotBeReplaced()
    {
        var seasonName = UniqueSeason();
        var organizer = await CreateUser();
        var member = await CreateUser();
        var outsider = await CreateUser();
        var league = await CreateLeague(seasonName, organizer.UserId!.Value, member.UserId!.Value);
        await Run(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            db.LeagueMembers.Add(new LeagueMember
            {
                LeagueId = league.LeagueId,
                UserId = outsider.UserId!.Value,
                Status = MembershipStatus.Pending,
                IsOrganizer = true
            });
            await db.SaveChangesAsync();
        });
        var first = await Send<ImportCatalogCommand, ListVersionView>(new(
            fixture.Admin, seasonName, Csv(Row(UniqueExternalId(), "Uno", "Uno", "P", "Roma"))));

        Assert.Null(await Query<GetLeagueCatalogQuery, ListVersionView?>(new(member, league.LeagueId, league.SeasonId)));

        var draft = await Assert.ThrowsAsync<DomainException>(() =>
            Send<SetLeagueCatalogCommand, LeagueCatalogView>(new(organizer, league.LeagueId, league.SeasonId, first.Id)));
        Assert.Equal(409, draft.StatusCode);
        await Send<PublishCatalogCommand, ListVersionView>(new(fixture.Admin, first.Id));

        Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() =>
            Send<SetLeagueCatalogCommand, LeagueCatalogView>(new(member, league.LeagueId, league.SeasonId, first.Id)))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() =>
            Query<GetLeagueCatalogQuery, ListVersionView?>(new(outsider, league.LeagueId, league.SeasonId)))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<DomainException>(() =>
            Send<SetLeagueCatalogCommand, LeagueCatalogView>(new(outsider, league.LeagueId, league.SeasonId, first.Id)))).StatusCode);

        var selections = await Task.WhenAll(
            Send<SetLeagueCatalogCommand, LeagueCatalogView>(new(organizer, league.LeagueId, league.SeasonId, first.Id)),
            Send<SetLeagueCatalogCommand, LeagueCatalogView>(new(organizer, league.LeagueId, league.SeasonId, first.Id)));
        Assert.All(selections, x => Assert.Equal(first.Id, x.ListVersionId));
        Assert.Equal(first.Id, (await Query<GetLeagueCatalogQuery, ListVersionView?>(new(member, league.LeagueId, league.SeasonId)))!.Id);

        var replacement = await Send<ImportCatalogCommand, ListVersionView>(new(
            fixture.Admin, seasonName, Csv(Row(UniqueExternalId(), "Due", "Due", "D", "Milan"))));
        await Send<PublishCatalogCommand, ListVersionView>(new(fixture.Admin, replacement.Id));
        Assert.Equal(409, (await Assert.ThrowsAsync<DomainException>(() =>
            Send<SetLeagueCatalogCommand, LeagueCatalogView>(new(fixture.Admin, league.LeagueId, league.SeasonId, replacement.Id)))).StatusCode);

        var caseSeasonName = "Season-" + UniqueSeason();
        var caseInsensitiveLeague = await CreateLeague(caseSeasonName, organizer.UserId!.Value, member.UserId!.Value);
        var caseVariant = await Send<ImportCatalogCommand, ListVersionView>(new(
            fixture.Admin,
            caseSeasonName.ToUpperInvariant(),
            Csv(Row(UniqueExternalId(), "Case", "Case", "C", "Roma"))));
        await Send<PublishCatalogCommand, ListVersionView>(new(fixture.Admin, caseVariant.Id));
        var selectedCaseVariant = await Send<SetLeagueCatalogCommand, LeagueCatalogView>(new(
            fixture.Admin, caseInsensitiveLeague.LeagueId, caseInsensitiveLeague.SeasonId, caseVariant.Id));
        Assert.Equal(caseVariant.Id, selectedCaseVariant.ListVersionId);

        var wrongSeason = await Send<ImportCatalogCommand, ListVersionView>(new(
            fixture.Admin, UniqueSeason(), Csv(Row(UniqueExternalId(), "Tre", "Tre", "A", "Inter"))));
        await Send<PublishCatalogCommand, ListVersionView>(new(fixture.Admin, wrongSeason.Id));
        var otherLeague = await CreateLeague(seasonName, organizer.UserId!.Value, member.UserId!.Value);
        Assert.Equal(404, (await Assert.ThrowsAsync<DomainException>(() =>
            Query<GetLeagueCatalogQuery, ListVersionView?>(new(fixture.Admin, league.LeagueId, otherLeague.SeasonId)))).StatusCode);
        Assert.Equal(404, (await Assert.ThrowsAsync<DomainException>(() =>
            Send<SetLeagueCatalogCommand, LeagueCatalogView>(new(fixture.Admin, league.LeagueId, otherLeague.SeasonId, first.Id)))).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<DomainException>(() =>
            Send<SetLeagueCatalogCommand, LeagueCatalogView>(new(fixture.Admin, otherLeague.LeagueId, otherLeague.SeasonId, wrongSeason.Id)))).StatusCode);
    }

    private async Task<(Guid LeagueId, Guid SeasonId)> CreateLeague(string seasonName, Guid organizerId, Guid memberId)
    {
        return await Run(async services =>
        {
            var db = services.GetRequiredService<FantasticheDbContext>();
            var league = new League { Name = "Lega " + Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow };
            var season = new LeagueSeason { LeagueId = league.Id, Name = seasonName };
            db.AddRange(
                league,
                season,
                new LeagueMember { LeagueId = league.Id, UserId = organizerId, Status = MembershipStatus.Active, IsOrganizer = true },
                new LeagueMember { LeagueId = league.Id, UserId = memberId, Status = MembershipStatus.Active });
            await db.SaveChangesAsync();
            return (league.Id, season.Id);
        });
    }

    private async Task<RequestContext> CreateUser()
    {
        return await Run(async services =>
        {
            var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var id = Guid.CreateVersion7();
            var email = id.ToString("N") + "@example.test";
            var user = new ApplicationUser { Id = id, Email = email, UserName = email, DisplayName = "Catalog user", EmailConfirmed = true };
            Assert.True((await manager.CreateAsync(user, "Catalog-User-123!")).Succeeded);
            return new RequestContext(id, false, "integration");
        });
    }

    private Task<TResponse> Send<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse>
        => Run(services => services.GetRequiredService<IRequestPublisher>().SendAsync<TRequest, TResponse>(request));

    private Task<TResponse> Query<TRequest, TResponse>(TRequest request)
        where TRequest : IRequest<TResponse>
        => Run(services => services.GetRequiredService<IRequestPublisher>().QueryAsync<TRequest, TResponse>(request));

    private async Task<T> Run<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    private async Task Run(Func<IServiceProvider, Task> work)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
    }

    private static string UniqueSeason() => DateTime.UtcNow.Year + "/" + Guid.NewGuid().ToString("N")[..8];

    private static string UniqueExternalId() => Interlocked.Increment(ref nextExternalId).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Csv(params string[] rows) => string.Join('\n', rows);

    private static string Row(string externalId, string name, string fullName, string role, string club)
    {
        var values = new[]
        {
            externalId, name, fullName, role, "Dc", "17", "16", "18", "15", club,
            "57", "60", "Destro", "Italia", "01/01/2000 00:00:00", "", "0", "", ""
        };
        return string.Join(',', values.Select(Escape));
    }

    private static string Escape(string value) => '"' + value.Replace("\"", "\"\"") + '"';
}
