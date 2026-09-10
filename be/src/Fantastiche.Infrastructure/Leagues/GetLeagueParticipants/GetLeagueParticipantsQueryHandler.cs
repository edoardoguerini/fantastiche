using System.Data;
using Dapper;
using Fantastiche.Core.Exceptions;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Infrastructure.Leagues;

public sealed class GetLeagueParticipantsQueryHandler(FantasticheDbContext db)
    : IRequestHandler<GetLeagueParticipantsQuery, LeagueParticipantsView>
{
    public async Task<LeagueParticipantsView> HandleAsync(GetLeagueParticipantsQuery request, CancellationToken ct)
    {
        if (request.Context.UserId is null || request.Context.UserId == Guid.Empty)
            throw new DomainException("auth.required", "Accesso richiesto.", 401);
        if (request.Page is < 1 or > 10000 || request.PageSize is < 1 or > 100)
            throw new DomainException("validation.invalid", "Paginazione non valida.");
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try
        {
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            var parameters = new { request.LeagueId, request.LeagueSeasonId, request.Context.UserId, Skip = (request.Page - 1) * request.PageSize, request.PageSize };
            var access = await connection.QuerySingleOrDefaultAsync<Access>(new CommandDefinition("""
                SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM AspNetUserRoles ur INNER JOIN AspNetRoles role ON role.Id = ur.RoleId
                    WHERE ur.UserId = @UserId AND role.NormalizedName = N'SUPERADMIN') THEN 1 ELSE 0 END AS bit) AS IsSuperAdmin,
                    CAST(COALESCE(member.IsOrganizer, 0) AS bit) AS IsOrganizer,
                    CAST(CASE WHEN member.UserId IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IsMember
                FROM LeagueSeasons season
                LEFT JOIN LeagueMembers member ON member.LeagueId = season.LeagueId AND member.UserId = @UserId AND member.Status = 1
                WHERE season.Id = @LeagueSeasonId AND season.LeagueId = @LeagueId;
                """, parameters, transaction, cancellationToken: ct))
                ?? throw new DomainException("resource.not_found", "Risorsa non disponibile.", 404);
            if (!access.IsSuperAdmin && !access.IsMember)
                throw new DomainException("auth.forbidden", "Operazione non consentita.", 403);
            var canManage = access.IsSuperAdmin || access.IsOrganizer;
            if (!canManage)
            {
                await transaction.CommitAsync(ct);
                return new(request.LeagueId, request.LeagueSeasonId, false, [], new([], 0, request.Page, request.PageSize));
            }
            using var result = await connection.QueryMultipleAsync(new CommandDefinition("""
                SELECT member.UserId, account.DisplayName, team.Name AS TeamName, member.IsOrganizer
                FROM LeagueMembers member
                INNER JOIN AspNetUsers account ON account.Id = member.UserId
                LEFT JOIN TeamMembers tm ON tm.UserId = member.UserId AND tm.LeagueId = member.LeagueId AND tm.LeagueSeasonId = @LeagueSeasonId
                LEFT JOIN Teams team ON team.Id = tm.TeamId AND team.LeagueId = @LeagueId AND team.LeagueSeasonId = @LeagueSeasonId
                WHERE member.LeagueId = @LeagueId AND member.Status = 1 AND (member.IsOrganizer = 1 OR tm.TeamId IS NOT NULL)
                ORDER BY account.DisplayName, member.UserId;
                SELECT COUNT(*) FROM LeagueInvitations WHERE LeagueId = @LeagueId AND LeagueSeasonId = @LeagueSeasonId;
                SELECT invitation.Id, account.DisplayName, invitation.Email,
                    CASE WHEN invitation.Kind = 0 THEN N'Organizer' ELSE N'Participant' END AS Kind,
                    CASE WHEN invitation.AcceptedAt IS NOT NULL THEN N'Accepted'
                         WHEN invitation.RevokedAt IS NOT NULL THEN N'Revoked'
                         WHEN invitation.ExpiresAt <= SYSUTCDATETIME() THEN N'Expired' ELSE N'Pending' END AS Status,
                    invitation.ExpiresAt
                FROM LeagueInvitations invitation INNER JOIN AspNetUsers account ON account.Id = invitation.UserId
                WHERE invitation.LeagueId = @LeagueId AND invitation.LeagueSeasonId = @LeagueSeasonId
                ORDER BY invitation.CreatedAt DESC, invitation.Id DESC
                OFFSET @Skip ROWS FETCH NEXT @PageSize ROWS ONLY;
                """, parameters, transaction, cancellationToken: ct));
            var participants = (await result.ReadAsync<LeagueParticipantView>()).AsList();
            var total = await result.ReadSingleAsync<int>();
            var invitations = (await result.ReadAsync<LeagueInvitationView>()).AsList();
            await transaction.CommitAsync(ct);
            return new(request.LeagueId, request.LeagueSeasonId, true, participants, new(invitations, total, request.Page, request.PageSize));
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private sealed record Access(bool IsSuperAdmin, bool IsOrganizer, bool IsMember);
}
