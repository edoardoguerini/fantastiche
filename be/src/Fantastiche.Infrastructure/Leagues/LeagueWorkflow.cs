using System.Data;
using System.Net;
using Fantastiche.Core.Auth;
using Fantastiche.Core.Email;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Emails;
using Fantastiche.Infrastructure.Teams;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
namespace Fantastiche.Infrastructure.Leagues;

public sealed class LeagueWorkflow(FantasticheDbContext db, UserManager<ApplicationUser> users,
 EmailPayloadProtector protector, TimeProvider clock, IConfiguration configuration)
{
    public async Task<LeagueDetails> CreateLeagueAsync(CreateLeagueCommand r, CancellationToken ct)
    {
        if (!r.Context.IsSuperAdmin || r.Context.UserId is null) throw Forbidden();
        ValidateRules(r);
        await using var tx = await BeginAsync(ct);
        var user = await GetOrCreateUserAsync(r.OrganizerEmail, r.OrganizerName);
        var league = new League { Name = Required(r.Name, 100), CreatedAt = clock.GetUtcNow() };
        var season = new LeagueSeason
        {
            LeagueId = league.Id,
            Name = Required(r.SeasonName, 50),
            Budget = r.Budget,
            Goalkeepers = r.Goalkeepers,
            Defenders = r.Defenders,
            Midfielders = r.Midfielders,
            Forwards = r.Forwards
        };
        db.AddRange(league, season, new LeagueMember { LeagueId = league.Id, UserId = user.Id });
        await db.SaveChangesAsync(ct);
        await QueueInvitationAsync(league, season.Id, user, r.Context.UserId.Value, InvitationKind.Organizer, ct);
        await tx.CommitAsync(ct);
        return Details(league, season);
    }
    public async Task<LeagueDetails> GetLeagueAsync(GetLeagueQuery r, CancellationToken ct)
    {
        await RequireMemberAsync(r.Context, r.LeagueId, false, ct);
        var league = await db.Leagues.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.LeagueId, ct) ?? throw NotFound();
        var season = await db.LeagueSeasons.AsNoTracking().Where(x => x.LeagueId == r.LeagueId).OrderByDescending(x => x.Id).FirstAsync(ct);
        return Details(league, season);
    }
    public async Task<InvitationDetails> InviteMemberAsync(InviteMemberCommand r, CancellationToken ct)
    {
        await using var tx = await BeginAsync(ct);
        await RequireMemberAsync(r.Context, r.LeagueId, true, ct);
        var league = await db.Leagues.SingleAsync(x => x.Id == r.LeagueId, ct);
        if (!await db.LeagueSeasons.AnyAsync(x => x.Id == r.LeagueSeasonId && x.LeagueId == r.LeagueId, ct)) throw NotFound();
        var user = await GetOrCreateUserAsync(r.Email, r.DisplayName);
        if (await db.TeamMembers.AnyAsync(x => x.LeagueSeasonId == r.LeagueSeasonId && x.UserId == user.Id, ct))
            throw new DomainException("team.already_member", "L’utente partecipa già alla stagione.", 409);
        if (await db.LeagueInvitations.AnyAsync(x => x.LeagueSeasonId == r.LeagueSeasonId && x.UserId == user.Id
          && x.Kind == InvitationKind.Participant && x.AcceptedAt == null && x.RevokedAt == null && x.ExpiresAt > clock.GetUtcNow(), ct))
            throw new DomainException("invitation.already_pending", "Esiste già un invito attivo. Usa il reinvio.", 409);
        if (!await db.LeagueMembers.AnyAsync(x => x.LeagueId == r.LeagueId && x.UserId == user.Id, ct))
            db.LeagueMembers.Add(new LeagueMember { LeagueId = r.LeagueId, UserId = user.Id });
        await db.SaveChangesAsync(ct);
        var invitation = await QueueInvitationAsync(league, r.LeagueSeasonId, user, r.Context.UserId!.Value, InvitationKind.Participant, ct);
        await tx.CommitAsync(ct);
        return invitation;
    }
    public async Task<InvitationPreview> GetInvitationAsync(GetInvitationQuery r, CancellationToken ct)
    {
        var hash = InvitationTokens.Hash(r.Token);
        var invitation = await db.LeagueInvitations.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct) ?? throw NotFound();
        ValidateAvailable(invitation);
        if (invitation.AcceptedAt is not null) throw new DomainException("invitation.consumed", "Invito già accettato.", 410);
        var league = await db.Leagues.AsNoTracking().SingleAsync(x => x.Id == invitation.LeagueId, ct);
        var user = await users.FindByIdAsync(invitation.UserId.ToString()) ?? throw NotFound();
        return new InvitationPreview(league.Name, invitation.ExpiresAt, await users.HasPasswordAsync(user), invitation.Kind == InvitationKind.Participant);
    }
    public async Task<AcceptanceDetails> AcceptInvitationAsync(AcceptInvitationCommand r, CancellationToken ct)
    {
        var hash = InvitationTokens.Hash(r.Token);
        await using var tx = await BeginAsync(ct);
        var invitation = await db.LeagueInvitations.SingleOrDefaultAsync(x => x.TokenHash == hash, ct) ?? throw NotFound();
        var user = await users.FindByIdAsync(invitation.UserId.ToString()) ?? throw NotFound();
        // Un replay richiede l’identità del destinatario e non modifica mai credenziali o squadra.
        if (invitation.AcceptedAt is not null)
        {
            if (r.Context.UserId != user.Id) throw Forbidden();
            return new AcceptanceDetails(invitation.LeagueId, invitation.LeagueSeasonId, invitation.AcceptedTeamId);
        }
        ValidateAvailable(invitation);
        if (r.Context.UserId is not null && r.Context.UserId != user.Id) throw Forbidden();
        if (await users.HasPasswordAsync(user))
        {
            if (r.Context.UserId != user.Id) throw new DomainException("invitation.login_required", "Accedi con l’account destinatario.", 401);
            if (!string.IsNullOrEmpty(r.Password)) throw new DomainException("invitation.password_not_allowed", "La password dell’account esistente non si modifica tramite invito.");
        }
        else
        {
            if (string.IsNullOrEmpty(r.Password)) throw new DomainException("invitation.password_required", "Imposta una password.");
            EnsureIdentity(await users.AddPasswordAsync(user, r.Password));
            user.EmailConfirmed = true;
            EnsureIdentity(await users.UpdateAsync(user));
        }
        var member = await db.LeagueMembers.SingleAsync(x => x.LeagueId == invitation.LeagueId && x.UserId == user.Id, ct);
        member.Status = MembershipStatus.Active;
        if (invitation.Kind == InvitationKind.Organizer) member.IsOrganizer = true;
        else
        {
            var name = Required(r.TeamName, 100);
            if (await db.TeamMembers.AnyAsync(x => x.LeagueSeasonId == invitation.LeagueSeasonId && x.UserId == user.Id, ct))
                throw new DomainException("team.already_member", "L’utente partecipa già alla stagione.", 409);
            var normalized = name.ToUpperInvariant();
            if (await db.Teams.AnyAsync(x => x.LeagueSeasonId == invitation.LeagueSeasonId && x.NormalizedName == normalized, ct))
                throw new DomainException("team.name_taken", "Nome squadra già usato.", 409);
            var season = await db.LeagueSeasons.SingleAsync(x => x.Id == invitation.LeagueSeasonId, ct);
            var team = new Team { LeagueId = invitation.LeagueId, LeagueSeasonId = season.Id, Name = name, NormalizedName = normalized, Budget = season.Budget };
            db.Teams.Add(team);
            db.TeamMembers.Add(new TeamMember { TeamId = team.Id, LeagueId = invitation.LeagueId, LeagueSeasonId = season.Id, UserId = user.Id });
            invitation.AcceptedTeamId = team.Id;
        }
        invitation.AcceptedAt = clock.GetUtcNow();
        await CancelEmailsAsync(invitation.Id, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new AcceptanceDetails(invitation.LeagueId, invitation.LeagueSeasonId, invitation.AcceptedTeamId);
    }
    public async Task<bool> RevokeInvitationAsync(RevokeInvitationCommand r, CancellationToken ct)
    {
        await using var tx = await BeginAsync(ct);
        await RequireMemberAsync(r.Context, r.LeagueId, true, ct);
        var invitation = await FindInvitationAsync(r.LeagueId, r.InvitationId, ct);
        if (invitation.AcceptedAt is not null) throw new DomainException("invitation.consumed", "Invito già accettato.", 409);
        invitation.RevokedAt ??= clock.GetUtcNow();
        await CancelEmailsAsync(invitation.Id, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
    public async Task<InvitationDetails> ResendInvitationAsync(ResendInvitationCommand r, CancellationToken ct)
    {
        await using var tx = await BeginAsync(ct);
        await RequireMemberAsync(r.Context, r.LeagueId, true, ct);
        var previous = await FindInvitationAsync(r.LeagueId, r.InvitationId, ct);
        if (previous.AcceptedAt is not null) throw new DomainException("invitation.consumed", "Invito già accettato.", 409);
        if (previous.RevokedAt is not null) throw new DomainException("invitation.revoked", "Invito revocato. Crea un nuovo invito.", 409);
        previous.RevokedAt = clock.GetUtcNow(); await CancelEmailsAsync(previous.Id, ct);
        var league = await db.Leagues.SingleAsync(x => x.Id == r.LeagueId, ct);
        var user = await users.FindByIdAsync(previous.UserId.ToString()) ?? throw NotFound();
        var result = await QueueInvitationAsync(league, previous.LeagueSeasonId, user, r.Context.UserId!.Value, previous.Kind, ct);
        await tx.CommitAsync(ct); return result;
    }
    private async Task<InvitationDetails> QueueInvitationAsync(League league, Guid seasonId, ApplicationUser user, Guid senderId, InvitationKind kind, CancellationToken ct)
    {
        var token = InvitationTokens.Create(); var now = clock.GetUtcNow();
        var invitation = new LeagueInvitation
        {
            LeagueId = league.Id,
            LeagueSeasonId = seasonId,
            UserId = user.Id,
            InvitedByUserId = senderId,
            Email = user.Email!,
            TokenHash = InvitationTokens.Hash(token),
            Kind = kind,
            CreatedAt = now,
            ExpiresAt = now.AddHours(72)
        };
        var baseUrl = configuration["Invitations:PublicBaseUrl"] ?? "http://localhost:6061";
        var link = baseUrl.TrimEnd('/') + "/invito#token=" + token;
        var safeName = WebUtility.HtmlEncode(league.Name); var safeLink = WebUtility.HtmlEncode(link);
        var email = new RenderedEmail(user.Email!, user.DisplayName, "Invito a " + league.Name,
         $"<p>Sei stato invitato alla lega {safeName}.</p><p><a href=\"{safeLink}\">Accetta invito</a></p><p>Il link scade tra 72 ore.</p>",
         $"Invito alla lega {league.Name}. Accetta entro 72 ore: {link}");
        db.LeagueInvitations.Add(invitation);
        db.EmailMessages.Add(new EmailMessage
        {
            InvitationId = invitation.Id,
            ToAddress = user.Email!,
            ProtectedPayload = protector.Protect(email),
            CreatedAt = now,
            NextAttemptAt = now
        });
        await db.SaveChangesAsync(ct);
        return new InvitationDetails(invitation.Id, league.Id, invitation.ExpiresAt);
    }
    private async Task<ApplicationUser> GetOrCreateUserAsync(string email, string displayName)
    {
        email = Required(email, 256);
        if (!System.Net.Mail.MailAddress.TryCreate(email, out var parsed) || parsed.Address != email)
            throw new DomainException("validation.email", "Indirizzo email non valido.");
        var user = await users.FindByEmailAsync(email);
        if (user is not null) return user;
        user = new ApplicationUser { UserName = email, Email = email, DisplayName = Required(displayName, 150), LockoutEnabled = true };
        EnsureIdentity(await users.CreateAsync(user)); return user;
    }
    private async Task RequireMemberAsync(RequestContext context, Guid leagueId, bool organizer, CancellationToken ct)
    {
        if (context.UserId is null) throw new DomainException("auth.required", "Accesso richiesto.", 401);
        // Il SuperAdmin gestisce anche il reinvio dell’invito iniziale, prima dell’adesione dell’organizzatore.
        if (context.IsSuperAdmin) return;
        if (!await db.LeagueMembers.AsNoTracking().AnyAsync(x => x.LeagueId == leagueId && x.UserId == context.UserId
          && x.Status == MembershipStatus.Active && (!organizer || x.IsOrganizer), ct)) throw Forbidden();
    }
    private async Task<IDbContextTransaction> BeginAsync(CancellationToken ct)
    {
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            // Serializza soltanto l’onboarding, poco frequente; l’asta avrà un lock distinto per asta.
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource = N'Fantastiche:Onboarding', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @r < 0 THROW 51000, 'Onboarding busy', 1;", ct);
            return tx;
        }
        catch { await tx.DisposeAsync(); throw; }
    }
    private async Task CancelEmailsAsync(Guid invitationId, CancellationToken ct)
    {
        var emails = await db.EmailMessages.Where(x => x.InvitationId == invitationId && (x.Status == EmailStatus.Pending || x.Status == EmailStatus.Processing)).ToListAsync(ct);
        foreach (var email in emails) { email.Status = EmailStatus.Cancelled; email.ProtectedPayload = ""; email.LeaseId = null; email.LeaseExpiresAt = null; }
    }
    private async Task<LeagueInvitation> FindInvitationAsync(Guid league, Guid id, CancellationToken ct) =>
     await db.LeagueInvitations.SingleOrDefaultAsync(x => x.Id == id && x.LeagueId == league, ct) ?? throw NotFound();
    private void ValidateAvailable(LeagueInvitation i)
    {
        if (i.RevokedAt is not null) throw new DomainException("invitation.revoked", "Invito revocato.", 410);
        if (i.ExpiresAt <= clock.GetUtcNow()) throw new DomainException("invitation.expired", "Invito scaduto.", 410);
    }
    private static string Required(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > max) throw new DomainException("validation.invalid", "Valore obbligatorio o troppo lungo.");
        return value.Trim();
    }
    private static void ValidateRules(CreateLeagueCommand r)
    {
        var slots = (long)r.Goalkeepers + r.Defenders + r.Midfielders + r.Forwards;
        if (r.Goalkeepers < 0 || r.Defenders < 0 || r.Midfielders < 0 || r.Forwards < 0 || slots <= 0 || slots > 100 || r.Budget < slots || r.Budget > 1000000)
            throw new DomainException("league.invalid_rules", "Budget e composizione rosa non validi.");
    }
    private static void EnsureIdentity(IdentityResult result)
    {
        if (!result.Succeeded) throw new DomainException("identity.validation", "Dati account non validi: " + string.Join("; ", result.Errors.Select(x => x.Code)));
    }
    private static DomainException Forbidden() => new("auth.forbidden", "Operazione non consentita.", 403);
    private static DomainException NotFound() => new("resource.not_found", "Risorsa non disponibile.", 404);
    private static LeagueDetails Details(League l, LeagueSeason s) => new(l.Id, l.Name, s.Id, s.Name, s.Budget, s.Goalkeepers, s.Defenders, s.Midfielders, s.Forwards);
}
