using System.Text.Json;
using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure;
using Fantastiche.Infrastructure.Auctions;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Leagues;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Runner esclusivamente locale: stessi handler del backend, nessuna modifica
// di password, privilegi, timer o tabelle dell'asta fuori dal motore di dominio.
var leagueId = Guid.Parse("01a09282-6460-7d01-822d-4c7bdd5f7925");
var seasonId = Guid.Parse("01a09282-6460-719f-b828-a39076de1eb1");
var excludedTeam = Guid.Parse("01a09282-6462-7ad7-b27a-bfff496b1317");
var watch = args.Contains("--watch");
var tie = args.Contains("--tie");
var bombOption = Array.IndexOf(args, "--bomb-id");
Guid? requestedBomb = bombOption >= 0 ? Guid.Parse(args[bombOption + 1]) : null;
if (args.Any(a => a.StartsWith("--") && a is not ("--watch" or "--check" or "--tie" or "--bomb-id")))
    throw new ArgumentException("Opzioni: --check, --watch, --tie, --bomb-id UUID");

var builder = Host.CreateApplicationBuilder();
if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Disponibile solo in Development.");
var connection = new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString("Fantastiche"));
if (connection.DataSource != "127.0.0.1,14333" || connection.InitialCatalog != "Fantastiche")
    throw new InvalidOperationException("Il runner accetta solo il database demo locale Fantastiche su 14333.");
builder.Logging.ClearProviders();
builder.Services.AddFantasticheInfrastructure(builder.Configuration);
using var host = builder.Build();
using var initialScope = host.Services.CreateScope();
var db = initialScope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
var session = await db.AuctionSessions.AsNoTracking().SingleAsync(s => s.LeagueId == leagueId && s.LeagueSeasonId == seasonId && s.Status != AuctionSessionStatus.Completed);
var rules = await db.LeagueSeasons.AsNoTracking().SingleAsync(s => s.Id == seasonId && s.LeagueId == leagueId);
var totalSlots = rules.Goalkeepers + rules.Defenders + rules.Midfielders + rules.Forwards;
var members = await (
    from tm in db.TeamMembers
    join team in db.Teams on tm.TeamId equals team.Id
    join user in db.Users on tm.UserId equals user.Id
    join lm in db.LeagueMembers on new { tm.LeagueId, tm.UserId } equals new { lm.LeagueId, lm.UserId }
    where tm.LeagueId == leagueId && tm.LeagueSeasonId == seasonId && lm.Status == MembershipStatus.Active
    select new Actor(team.Id, team.Name, user.Id, user.Email!)).ToListAsync();
if (!members.Any(m => m.TeamId == excludedTeam && m.Email == "luca.ferri@example.test"))
    throw new InvalidOperationException("La squadra esclusa non corrisponde a Luca: esecuzione interrotta.");
var actors = members.Where(m => m.TeamId != excludedTeam && m.Email != "luca.ferri@example.test" && m.Email.EndsWith("@example.test", StringComparison.OrdinalIgnoreCase))
    .GroupBy(m => m.TeamId).Select(g => g.OrderBy(m => m.Email).First()).OrderBy(m => m.TeamName).ToArray();
if (actors.Length != 7) throw new InvalidOperationException($"Attesi sette account demo, trovati {actors.Length}.");
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(30));
Console.CancelKeyPress += (_, e) => { e.Cancel = true; deadline.Cancel(); };
var ct = deadline.Token;

RequestContext Context(Actor actor) => new(actor.UserId, false, "local-bomb-test");
async Task<AuctionSessionView> State()
{
    using var scope = host.Services.CreateScope();
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeout.CancelAfter(TimeSpan.FromSeconds(5));
    return await scope.ServiceProvider.GetRequiredService<IRequestPublisher>()
        .QueryAsync<GetAuctionStateQuery, AuctionSessionView>(new(Context(actors[0]), session.Id), timeout.Token);
}
var initial = await State();
if (actors.Any(a => !initial.TeamOrder.Contains(a.TeamId)))
    throw new InvalidOperationException("Una squadra demo non partecipa alla sessione attiva.");
for (var i = 0; i < actors.Length; i++)
    Console.WriteLine($"{actors[i].TeamName}: {(tie && i >= actors.Length - 2 ? 35 : (i + 1) * 5)} crediti");
Console.WriteLine("ESCLUSA: Atletico Spritz / Luca Ferri. Nessun invio per questa squadra.");
Console.WriteLine($"Sessione {session.Id}; stato {initial.Status}; versione {initial.Version}.");
if (!watch) { Console.WriteLine("CHECK OK — nessuna offerta inviata. Usare --watch per attendere la prossima Bomba."); return; }
if (requestedBomb is null && initial.CurrentBomb is { Status: "Waiting" or "Collecting" or "Revealing" })
    throw new InvalidOperationException("Bomba già aperta: indicare --bomb-id per partecipare esplicitamente a quella Bomba.");
if (requestedBomb is not null && initial.CurrentBomb?.Id != requestedBomb)
    throw new InvalidOperationException("La Bomba indicata non è quella corrente.");

var journal = Path.Combine(Directory.GetCurrentDirectory(), ".local", "bomb-test");
Directory.CreateDirectory(journal);
var attempted = new HashSet<(Guid Bomb, int Round, Guid Team)>();
Guid? target = requestedBomb;
var baseline = initial.CurrentBomb?.Id;
var lastStatus = "";
Console.WriteLine("ARMATO — attendo la prossima Bomba, massimo 30 minuti. Stop: Ctrl+C.");

async Task Submit(Actor actor, AuctionBombView bomb, int amount)
{
    if (actor.TeamId == excludedTeam || actor.Email.Equals("luca.ferri@example.test", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Invio per Luca vietato.");
    var path = Path.Combine(journal, $"{bomb.Id}-{bomb.Round}-{actor.TeamId}.json");
    Attempt command;
    var recoverOnly = File.Exists(path);
    if (recoverOnly) command = JsonSerializer.Deserialize<Attempt>(await File.ReadAllTextAsync(path, ct))!;
    else
    {
        command = new(Guid.NewGuid(), actor.UserId, actor.TeamId, bomb.Id, bomb.Round, amount);
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(file, command, cancellationToken: ct);
        file.Flush(true);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    if (command.UserId != actor.UserId || command.TeamId != actor.TeamId || command.BombId != bomb.Id || command.Round != bomb.Round)
        throw new InvalidOperationException("Registro locale incoerente: nessun invio.");
    using var scope = host.Services.CreateScope();
    var publisher = scope.ServiceProvider.GetRequiredService<IRequestPublisher>();
    AuctionCommandResult? receipt = null;
    try
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        receipt = recoverOnly
            ? await publisher.QueryAsync<GetAuctionReceiptQuery, AuctionCommandResult>(new(Context(actor), session.Id, command.RequestId), timeout.Token)
            : await publisher.SendAsync<SubmitBombOfferCommand, AuctionCommandResult>(new(Context(actor), session.Id, command.RequestId, bomb.Id, bomb.Round, command.Amount), timeout.Token);
    }
    catch (Exception) when (!ct.IsCancellationRequested)
    {
        // Una risposta persa viene solo verificata: mai un reinvio automatico.
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            receipt = await publisher.QueryAsync<GetAuctionReceiptQuery, AuctionCommandResult>(new(Context(actor), session.Id, command.RequestId), timeout.Token);
        }
        catch (Exception) when (!ct.IsCancellationRequested) { }
    }
    Console.WriteLine($"{actor.TeamName}: {command.Amount} crediti — {(receipt?.Accepted == true ? "CONFERMATA" : receipt is null ? "ESITO INCERTO, nessun reinvio" : $"RIFIUTATA ({receipt.ErrorCode})")}");
    await File.WriteAllTextAsync(path + ".result.json", JsonSerializer.Serialize(receipt), ct);
}

try
{
    while (!ct.IsCancellationRequested)
    {
        AuctionSessionView state;
        try { state = await State(); }
        catch (Exception) when (!ct.IsCancellationRequested)
        { Console.WriteLine("Stato momentaneamente non disponibile: attendo, senza inviare offerte."); await Task.Delay(1000, ct); continue; }
        var bomb = state.CurrentBomb;
        if (target is null && bomb is not null && bomb.Id != baseline) target = bomb.Id;
        if (target is not null)
        {
            if (bomb is null || bomb.Id != target) { Console.WriteLine("Bomba cambiata: runner terminato."); break; }
            var status = $"{bomb.Status}/{bomb.Round}";
            if (status != lastStatus)
            {
                lastStatus = status;
                Console.WriteLine($"Bomba {bomb.Name}: {bomb.Status}, turno {bomb.Round}, scadenza server {bomb.Deadline:O}.");
            }
            if (bomb.Status is "Completed" or "Cancelled" or "NoSale")
            { Console.WriteLine($"FINITO — {bomb.Status}. Il runner non partecipa ad altre Bombe."); break; }
            if (bomb.Status == "Collecting" && bomb.Deadline > state.ServerTime)
            {
                var jobs = new List<Task>();
                for (var i = 0; i < actors.Length; i++)
                {
                    var actor = actors[i];
                    if (!bomb.Participants.Any(p => p.TeamId == actor.TeamId && !p.HasSubmitted)) continue;
                    var team = state.Teams.Single(t => t.Id == actor.TeamId);
                    var maximum = team.Budget - Math.Max(0, totalSlots - team.Goalkeepers - team.Defenders - team.Midfielders - team.Forwards - 1);
                    var planned = bomb.Round == 1 ? (tie && i >= actors.Length - 2 ? 35 : (i + 1) * 5) : bomb.MinimumAmount + i * 5;
                    var amount = Math.Min(maximum, Math.Max(planned, bomb.MinimumAmount));
                    if (amount < bomb.MinimumAmount) continue;
                    if (attempted.Add((bomb.Id, bomb.Round, actor.TeamId))) jobs.Add(Submit(actor, bomb, amount));
                }
                await Task.WhenAll(jobs);
            }
        }
        await Task.Delay(500, ct);
    }
}
catch (OperationCanceledException) { Console.WriteLine("Runner fermato. Nessun altro invio."); }

record Actor(Guid TeamId, string TeamName, Guid UserId, string Email);
record Attempt(Guid RequestId, Guid UserId, Guid TeamId, Guid BombId, int Round, int Amount);
