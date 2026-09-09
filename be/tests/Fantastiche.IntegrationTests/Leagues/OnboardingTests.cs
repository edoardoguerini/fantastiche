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
        var league = await Workflow(w => w.CreateLeagueAsync(new(fixture.Admin, "Lega test", "2026/27", email, "Organizzatore"), default));
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
        return text.Split("#token=")[1];
    }
    private async Task<RequestContext> ActivateOrganizer((LeagueDetails League, Guid UserId, string Token) setup)
    {
        await Workflow(w => w.AcceptInvitationAsync(new(Anonymous, setup.Token, Password, null), default));
        return new RequestContext(setup.UserId, false, "integration");
    }
    [Fact]
    public async Task OnlySuperAdminCreatesLeague()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.CreateLeagueAsync(new(Anonymous, "Lega", "2026", "user@example.test", "Utente"), default)));
        Assert.Equal(403, error.StatusCode);
    }
    [Fact]
    public async Task OrganizerInvitationActivatesMembershipWithoutTeamAndPreviewDoesNotConsume()
    {
        var s = await CreateLeague();
        var preview = await Workflow(w => w.GetInvitationAsync(new(Anonymous, s.Token), default));
        Assert.False(preview.RequiresLogin); Assert.False(preview.RequiresTeam);
        await Run(async p =>
        {
            var db = p.GetRequiredService<FantasticheDbContext>();
            Assert.Null((await db.LeagueInvitations.SingleAsync(x => x.LeagueId == s.League.Id)).AcceptedAt);
            Assert.False(await p.GetRequiredService<UserManager<ApplicationUser>>().HasPasswordAsync((await db.Users.FindAsync(s.UserId))!));
            var queued = await db.EmailMessages.SingleAsync(x => db.LeagueInvitations.Where(i => i.LeagueId == s.League.Id).Select(i => i.Id).Contains(x.InvitationId));
            Assert.DoesNotContain(s.Token, queued.ProtectedPayload); return true;
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
        var s = await CreateLeague(); var organizer = await ActivateOrganizer(s);
        var invitation = await Workflow(w => w.InviteMemberAsync(new(organizer, s.League.Id, s.League.LeagueSeasonId, Guid.NewGuid() + "@example.test", "Partecipante"), default));
        var token = await Run(p => Token(p, invitation.Id));
        var result = await Workflow(w => w.AcceptInvitationAsync(new(Anonymous, token, Password, "Squadra uno"), default));
        Assert.NotNull(result.TeamId);
        var id = await Run(async p => (await p.GetRequiredService<FantasticheDbContext>().LeagueInvitations.FindAsync(invitation.Id))!.UserId);
        var replay = await Workflow(w => w.AcceptInvitationAsync(new(new(id, false, "replay"), token, null, "Altro nome"), default));
        Assert.Equal(result, replay);
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.AcceptInvitationAsync(new(Anonymous, token, Password, "Altro"), default)));
        await Run(async p =>
        {
            var db = p.GetRequiredService<FantasticheDbContext>();
            Assert.Single(await db.Teams.Where(x => x.LeagueId == s.League.Id).ToListAsync());
            Assert.Equal(500, (await db.Teams.FindAsync(result.TeamId))!.Budget);
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
        var invitation = await Workflow(w => w.InviteMemberAsync(new(organizer, s.League.Id, s.League.LeagueSeasonId, email, "Nome ignorato"), default));
        var token = await Run(p => Token(p, invitation.Id));
        var error = await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.AcceptInvitationAsync(new(Anonymous, token, null, "Squadra"), default)));
        Assert.Equal(401, error.StatusCode);
        var ctx = new RequestContext(userId, false, "integration");
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.AcceptInvitationAsync(new(ctx, token, "Different-Password-12!", "Squadra"), default)));
        await Workflow(w => w.AcceptInvitationAsync(new(ctx, token, null, "Squadra"), default));
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
        var invite = await Workflow(w => w.InviteMemberAsync(new(organizer, s.League.Id, s.League.LeagueSeasonId, Guid.NewGuid() + "@example.test", "User"), default));
        var old = await Run(p => Token(p, invite.Id));
        var resent = await Workflow(w => w.ResendInvitationAsync(new(organizer, s.League.Id, invite.Id), default));
        Assert.NotEqual(invite.Id, resent.Id);
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.GetInvitationAsync(new(Anonymous, old), default)));
        var current = await Run(p => Token(p, resent.Id));
        await Run(async p => { var db = p.GetRequiredService<FantasticheDbContext>(); (await db.LeagueInvitations.FindAsync(resent.Id))!.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1); await db.SaveChangesAsync(); return true; });
        var error = await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.AcceptInvitationAsync(new(Anonymous, current, Password, "Team"), default)));
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
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.InviteMemberAsync(new(organizer, b.League.Id, b.League.LeagueSeasonId, "nobody@example.test", "User"), default)));
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.InviteMemberAsync(new(organizer, a.League.Id, b.League.LeagueSeasonId, "nobody@example.test", "User"), default)));
    }
    [Fact]
    public async Task ConcurrentDuplicateInvitesCreateOneAccountOneMembershipAndOneEmail()
    {
        var s = await CreateLeague(); var ctx = await ActivateOrganizer(s); var email = Guid.NewGuid() + "@example.test";
        async Task<bool> Invite()
        {
            try { await Workflow(w => w.InviteMemberAsync(new(ctx, s.League.Id, s.League.LeagueSeasonId, email, "User"), default)); return true; }
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
            var i = await Workflow(w => w.InviteMemberAsync(new(ctx, s.League.Id, s.League.LeagueSeasonId, Guid.NewGuid() + "@example.test", "User"), default));
            return (i.Id, await Run(p => Token(p, i.Id)));
        }
        var a = await Invite(); var b = await Invite();
        await Workflow(w => w.AcceptInvitationAsync(new(Anonymous, a.Token, Password, "Same team"), default));
        await Assert.ThrowsAsync<DomainException>(() => Workflow(w => w.AcceptInvitationAsync(new(Anonymous, b.Token, Password, "same team"), default)));
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
        var invitation = await Workflow(w => w.InviteMemberAsync(new(ctx, s.League.Id, s.League.LeagueSeasonId, Guid.NewGuid() + "@example.test", "User"), default));
        var token = await Run(p => Token(p, invitation.Id));
        async Task<bool> Accept()
        {
            try { await Workflow(w => w.AcceptInvitationAsync(new(Anonymous, token, Password, "Una squadra"), default)); return true; }
            catch (DomainException e) when (e.StatusCode == 403) { return false; }
        }
        var results = await Task.WhenAll(Accept(), Accept()); Assert.Single(results, x => x);
        await Run(async p => { var db = p.GetRequiredService<FantasticheDbContext>(); Assert.Single(await db.Teams.Where(x => x.LeagueId == s.League.Id).ToListAsync()); return true; });
    }

}
