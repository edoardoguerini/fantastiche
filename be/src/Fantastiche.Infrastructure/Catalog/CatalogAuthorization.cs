using Fantastiche.Core.Auth;
using Fantastiche.Infrastructure.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fantastiche.Infrastructure.Catalog;

internal static class CatalogAuthorization
{
    internal static async Task<bool> IsSuperAdminAsync(FantasticheDbContext db, RequestContext context, CancellationToken ct)
    {
        if (!context.IsSuperAdmin || context.UserId is null || context.UserId == Guid.Empty) return false;
        // La claim abilita il controllo, ma una revoca nel database ha effetto sul cookie già emesso.
        return await db.UserRoles.AsNoTracking().AnyAsync(userRole => userRole.UserId == context.UserId
            && db.Roles.Any(role => role.Id == userRole.RoleId && role.NormalizedName == "SUPERADMIN"), ct);
    }
}
