using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Leagues;
using Fantastiche.Infrastructure.Teams;
using Fantastiche.Infrastructure.Emails;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Auctions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Fantastiche.Infrastructure.Common.Persistence;

public sealed class FantasticheDbContext(DbContextOptions<FantasticheDbContext> options)
 : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<League> Leagues => Set<League>();
    public DbSet<LeagueSeason> LeagueSeasons => Set<LeagueSeason>();
    public DbSet<LeagueMember> LeagueMembers => Set<LeagueMember>();
    public DbSet<LeagueInvitation> LeagueInvitations => Set<LeagueInvitation>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<ListVersion> ListVersions => Set<ListVersion>();
    public DbSet<ListEntry> ListEntries => Set<ListEntry>();
    public DbSet<AuctionSession> AuctionSessions => Set<AuctionSession>();
    public DbSet<CallOrderEntry> CallOrderEntries => Set<CallOrderEntry>();
    public DbSet<PlayerAuction> PlayerAuctions => Set<PlayerAuction>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<RosterEntry> RosterEntries => Set<RosterEntry>();
    public DbSet<BudgetMovement> BudgetMovements => Set<BudgetMovement>();
    public DbSet<CommandReceipt> CommandReceipts => Set<CommandReceipt>();
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().Property(x => x.DisplayName).HasMaxLength(150);
        builder.Entity<ApplicationUser>().HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
        builder.ApplyConfigurationsFromAssembly(typeof(FantasticheDbContext).Assembly);
    }
}
