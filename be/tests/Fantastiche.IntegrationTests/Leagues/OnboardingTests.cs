using System.Text.RegularExpressions;
using Fantastiche.Core.Auth;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Emails;
using Fantastiche.Infrastructure.Leagues;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Fantastiche.IntegrationTests.Leagues;

public sealed class OnboardingTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    private static readonly RequestContext Anonymous = new(null, false, "integration");
    private const string Password = "Invited-User-123!";
    private async Task<T> Run<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = fixture.Services.CreateAsyncScope(); return await work(scope.ServiceProvider);
    }
    private Task<T> Workflow<T>(Func<LeagueWorkflow, Task<T>> work) => Run(p => work(p.GetRequiredService<LeagueWorkflow>()));
    private async Task<(LeagueDetails League, Guid UserId, string Token)> CreateLeague()
    {
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        var league = await Workflow(w => w.CreateLeagueAsync(new(fixture.Admin, "Lega test", "2026/27", email), default));
        return await Run(async p =>
        {
            var db = p.GetRequiredService<FantasticheDbContext>();
            var invite = await db.LeagueInvitations.SingleAsync(x => x.LeagueId == league.Id);
            return (league, invite.UserId, await Token(p, invite.Id));
        });
    }
    private static async Task<string> Token(IServiceProvider p, Guid id)
    {
        var email = await p.GetRequiredService<FantasticheDbContext>().EmailMessages.SingleAsync(x => x.InvitationId == id);
        var text = p.GetRequiredService<EmailPayloadProtector>().Unprotect(email.ProtectedPayload).TextBody!;
        return Regex.Match(text, "#token=([0-9a-fA-F]{64})").Groups[1].Value;
    }
    private async Task<RequestContext> ActivateOrganizer((LeagueDetails League, Guid UserId, string Token) setup)
    {
        var result = await Workflow(w => w.AcceptInvitationAsync(new(Anonymous, setup.Token, Password, null, "Organizzatore"), default));
        Assert.Null(result.TeamId); Assert.Null(result.TeamName); Assert.EndsWith("@example.test", result.Email);
        return new RequestContext(setup.UserId, false, "integration");
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NewAccountRequiresOwnNameBeforeActivating(string? name)
    {
        var s = await CreateLeague();
        var error = await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.AcceptInvitationAsync(new(Anonymous, s.Token, Password, null, name), default)));
        Assert.Equal("invitation.name_required", error.Code);
        await Run(async p =>
        {
            var db = p.GetRequiredService<FantasticheDbContext>();
            var user = (await db.Users.FindAsync(s.UserId))!;
            Assert.Empty(user.DisplayName);
            Assert.Null(user.PasswordHash);
            Assert.False(user.EmailConfirmed);
            Assert.Null((await db.LeagueInvitations.SingleAsync(x => x.LeagueId == s.League.Id)).AcceptedAt);
            return true;
        });
    }

    [Fact]
    public async Task SuperAdminClaimWithoutDatabaseRoleCannotCreateLeague()
    {
        var email = Guid.NewGuid() + "@example.test";
        var userId = await Run(async p =>
        {
            var user = new ApplicationUser { Email = email, UserName = email, DisplayName = "Non admin" };
            Assert.True((await p.GetRequiredService<UserManager<ApplicationUser>>().CreateAsync(user)).Succeeded);
            return user.Id;
        });
        var context = new RequestContext(userId, true, "stale-claim");
        var error = await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.CreateLeagueAsync(
            new(context, "Lega non autorizzata", "2026/27", email), default)));
        Assert.Equal(403, error.StatusCode);
        await Run(async p =>
        {
            Assert.False(await p.GetRequiredService<FantasticheDbContext>().LeagueMembers.AnyAsync(x => x.UserId == userId));
            return true;
        });
    }

    [Fact]
    public async Task OnlySuperAdminCreatesLeague()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.CreateLeagueAsync(new(Anonymous, "Lega", "2026", "user@example.test"), default)));
        Assert.Equal(403, error.StatusCode);
    }
    [Fact]
    public async Task OrganizerInvitationActivatesMembershipWithoutTeamAndPreviewDoesNotConsume()
    {
        var s = await CreateLeague();
        var preview = await Workflow(w => w.GetInvitationAsync(new(Anonymous, s.Token), default));
        Assert.False(preview.RequiresLogin); Assert.False(preview.RequiresTeam);
        Assert.Equal("Lega test", preview.LeagueName); Assert.Null(preview.LeagueLogoUrl); Assert.Equal("Admin", preview.InvitedBy);
        await Run(async p =>
        {
            var db = p.GetRequiredService<FantasticheDbContext>();
            var recipient = (await db.Users.FindAsync(s.UserId))!.Email!;
            Assert.Equal(EmailMasking.Mask(recipient), preview.RecipientEmailHint); Assert.DoesNotContain(recipient, preview.RecipientEmailHint);
            Assert.Null((await db.LeagueInvitations.SingleAsync(x => x.LeagueId == s.League.Id)).AcceptedAt);
            Assert.False(await p.GetRequiredService<UserManager<ApplicationUser>>().HasPasswordAsync((await db.Users.FindAsync(s.UserId))!));
            var queued = await db.EmailMessages.SingleAsync(x => db.LeagueInvitations.Where(i => i.LeagueId == s.League.Id).Select(i => i.Id).Contains(x.InvitationId));
            Assert.DoesNotContain(s.Token, queued.ProtectedPayload);
            var email = p.GetRequiredService<EmailPayloadProtector>().Unprotect(queued.ProtectedPayload);
            Assert.Contains("Ciao, la tua lega ti aspetta.", email.TextBody);
            Assert.Contains("il tuo nome", email.TextBody);
            Assert.Empty((await db.Users.FindAsync(s.UserId))!.DisplayName);
            return true;
        });
        var ctx = await ActivateOrganizer(s);
        var detail = await Workflow(w => w.GetLeagueAsync(new(ctx, s.League.Id), default));
        Assert.Equal(500, detail.Budget);
        await Run(async p =>
        {
            var db = p.GetRequiredService<FantasticheDbContext>(); var m = await db.LeagueMembers.FindAsync(s.League.Id, s.UserId);
            Assert.True(m!.IsOrganizer); Assert.Equal(MembershipStatus.Active, m.Status);
            Assert.False(await db.Teams.AnyAsync(x => x.LeagueId == s.League.Id)); return true;
        });
    }
    [Fact]
    public async Task NewParticipantAcceptsAtomicallyAndAuthenticatedReplayDoesNotDuplicateTeam()
    {
        var s = await CreateLeague(); var organizer = await ActivateOrganizer(s); var email = Guid.NewGuid() + "@example.test";
        var invitation = await Workflow(w => w.InviteMemberAsync(new(organizer, s.League.Id, s.League.LeagueSeasonId, email), default));
        var token = await Run(p => Token(p, invitation.Id));
        var preview = await Workflow(w => w.GetInvitationAsync(new(Anonymous, token), default));
        Assert.True(preview.RequiresTeam); Assert.False(preview.RequiresLogin); Assert.Equal("Organizzatore", preview.InvitedBy);
        Assert.Equal(EmailMasking.Mask(email), preview.RecipientEmailHint);
        var result = await Workflow(w => w.AcceptInvitationAsync(new(Anonymous, token, Password, "Squadra uno", "  Partecipante  "), default));
        Assert.NotNull(result.TeamId); Assert.Equal("Squadra uno", result.TeamName); Assert.Equal(email, result.Email);
        var id = await Run(async p => (await p.GetRequiredService<FantasticheDbContext>().LeagueInvitations.FindAsync(invitation.Id))!.UserId);
        var replay = await Workflow(w => w.AcceptInvitationAsync(new(new(id, false, "replay"), token, null, "Altro nome", "Nome replay"), default));
        Assert.Equal(result, replay);
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.AcceptInvitationAsync(new(Anonymous, token, Password, "Altro", "Partecipante"), default)));
        await Run(async p =>
        {
            var db = p.GetRequiredService<FantasticheDbContext>();
            Assert.Single(await db.Teams.Where(x => x.LeagueId == s.League.Id).ToListAsync());
            Assert.Equal(500, (await db.Teams.FindAsync(result.TeamId))!.Budget);
            Assert.Equal("Partecipante", (await db.Users.FindAsync(id))!.DisplayName);
            Assert.Equal(EmailStatus.Cancelled, (await db.EmailMessages.SingleAsync(x => x.InvitationId == invitation.Id)).Status); return true;
        });
    }
    [Fact]
    public async Task ExistingAccountMustLoginAndInvitationNeverResetsPassword()
    {
        var s = await CreateLeague(); var organizer = await ActivateOrganizer(s);
        var email = Guid.NewGuid() + "@example.test";
        var userId = await Run(async p =>
        {
            var manager = p.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Email = email, UserName = email, DisplayName = "Esistente", EmailConfirmed = true };
            Assert.True((await manager.CreateAsync(user, Password)).Succeeded); return user.Id;
        });
        var invitation = await Workflow(w => w.InviteMemberAsync(new(organizer, s.League.Id, s.League.LeagueSeasonId, email), default));
        var token = await Run(p => Token(p, invitation.Id));
        await Run(async p =>
        {
            var queued = await p.GetRequiredService<FantasticheDbContext>().EmailMessages.SingleAsync(x => x.InvitationId == invitation.Id);
            var email = p.GetRequiredService<EmailPayloadProtector>().Unprotect(queued.ProtectedPayload);
            Assert.Contains("Ciao Esistente,", email.TextBody);
            Assert.Contains("Accedi con il tuo account", email.TextBody);
            Assert.DoesNotContain("password", email.TextBody);
            return true;
        });
        var error = await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.AcceptInvitationAsync(new(Anonymous, token, null, "Squadra"), default)));
        Assert.Equal(401, error.StatusCode);
        var ctx = new RequestContext(userId, false, "integration");
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.AcceptInvitationAsync(new(ctx, token, "Different-Password-12!", "Squadra"), default)));
        await Workflow(w => w.AcceptInvitationAsync(new(ctx, token, null, "Squadra", "Nome manomesso"), default));
        await Run(async p =>
        {
            var manager = p.GetRequiredService<UserManager<ApplicationUser>>(); var user = (await manager.FindByIdAsync(userId.ToString()))!;
            Assert.True(await manager.CheckPasswordAsync(user, Password)); Assert.Equal("Esistente", user.DisplayName); return true;
        });
    }
    [Fact]
    public async Task RevokeAndResendInvalidateOldTokensAndExpiredInvitationCannotActivate()
    {
        var s = await CreateLeague(); var organizer = await ActivateOrganizer(s);
        var invite = await Workflow(w => w.InviteMemberAsync(new(organizer, s.League.Id, s.League.LeagueSeasonId, Guid.NewGuid() + "@example.test"), default));
        var old = await Run(p => Token(p, invite.Id));
        var resent = await Workflow(w => w.ResendInvitationAsync(new(organizer, s.League.Id, invite.Id), default));
        Assert.NotEqual(invite.Id, resent.Id);
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.GetInvitationAsync(new(Anonymous, old), default)));
        var current = await Run(p => Token(p, resent.Id));
        await Run(async p => { var db = p.GetRequiredService<FantasticheDbContext>(); (await db.LeagueInvitations.FindAsync(resent.Id))!.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); return true; });
        var error = await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.AcceptInvitationAsync(new(Anonymous, current, Password, "Team", "Partecipante"), default)));
        Assert.Equal("invitation.expired", error.Code);
        await Workflow(w => w.RevokeInvitationAsync(new(organizer, s.League.Id, resent.Id), default));
        await Run(async p => { var db = p.GetRequiredService<FantasticheDbContext>(); Assert.Equal(EmailStatus.Cancelled, (await db.EmailMessages.SingleAsync(x => x.InvitationId == resent.Id)).Status); return true; });
    }
    [Fact]
    public async Task PendingAndCrossLeagueUsersCannotReadOrInvite()
    {
        var a = await CreateLeague(); var b = await CreateLeague(); var organizer = await ActivateOrganizer(a);
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.GetLeagueAsync(new(new(b.UserId, false, "pending"), b.League.Id), default)));
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.GetLeagueAsync(new(organizer, b.League.Id), default)));
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.InviteMemberAsync(new(organizer, b.League.Id, b.League.LeagueSeasonId, "nobody@example.test"), default)));
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.InviteMemberAsync(new(organizer, a.League.Id, b.League.LeagueSeasonId, "nobody@example.test"), default)));
    }
    [Fact]
    public async Task ConcurrentDuplicateInvitesCreateOneAccountOneMembershipAndOneEmail()
    {
        var s = await CreateLeague(); var ctx = await ActivateOrganizer(s); var email = Guid.NewGuid() + "@example.test";
        async Task<bool> Invite()
        {
            try { await Workflow(w => w.InviteMemberAsync(new(ctx, s.League.Id, s.League.LeagueSeasonId, email), default)); return true; }
            catch (DomainException e) when (e.Code == "invitation.already_pending") { return false; }
        }
        var results = await Task.WhenAll(Invite(), Invite()); Assert.Single(results, x => x);
        await Run(async p =>
        {
            var db = p.GetRequiredService<FantasticheDbContext>(); var u = await db.Users.SingleAsync(x => x.Email == email);
            Assert.Single(await db.LeagueMembers.Where(x => x.LeagueId == s.League.Id && x.UserId == u.Id).ToListAsync());
            Assert.Single(await db.LeagueInvitations.Where(x => x.LeagueId == s.League.Id && x.UserId == u.Id).ToListAsync()); return true;
        });
    }
    [Fact]
    public async Task TeamConflictRollsBackNewPasswordAndMembershipActivation()
    {
        var s = await CreateLeague(); var ctx = await ActivateOrganizer(s);
        async Task<(Guid Id, string Token)> Invite()
        {
            var i = await Workflow(w => w.InviteMemberAsync(new(ctx, s.League.Id, s.League.LeagueSeasonId, Guid.NewGuid() + "@example.test"), default));
            return (i.Id, await Run(p => Token(p, i.Id)));
        }
        var a = await Invite(); var b = await Invite();
        await Workflow(w => w.AcceptInvitationAsync(new(Anonymous, a.Token, Password, "Same team", "Partecipante"), default));
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.AcceptInvitationAsync(new(Anonymous, b.Token, Password, "same team", "Partecipante"), default)));
        await Run(async p =>
        {
            var db = p.GetRequiredService<FantasticheDbContext>(); var invite = (await db.LeagueInvitations.FindAsync(b.Id))!;
            Assert.Null(invite.AcceptedAt); Assert.Null((await db.Users.FindAsync(invite.UserId))!.PasswordHash);
            Assert.Equal(MembershipStatus.Pending, (await db.LeagueMembers.FindAsync(s.League.Id, invite.UserId))!.Status); return true;
        });
    }
    [Fact]
    public async Task ConcurrentAcceptancesConsumeTokenAndCreateTeamOnce()
    {
        var s = await CreateLeague(); var ctx = await ActivateOrganizer(s);
        var invitation = await Workflow(w => w.InviteMemberAsync(new(ctx, s.League.Id, s.League.LeagueSeasonId, Guid.NewGuid() + "@example.test"), default));
        var token = await Run(p => Token(p, invitation.Id));
        async Task<bool> Accept()
        {
            try { await Workflow(w => w.AcceptInvitationAsync(new(Anonymous, token, Password, "Una squadra", "Partecipante"), default)); return true; }
            catch (DomainException e) when (e.StatusCode == 403) { return false; }
        }
        var results = await Task.WhenAll(Accept(), Accept()); Assert.Single(results, x => x);
        await Run(async p => { var db = p.GetRequiredService<FantasticheDbContext>(); Assert.Single(await db.Teams.Where(x => x.LeagueId == s.League.Id).ToListAsync()); return true; });
    }

}
