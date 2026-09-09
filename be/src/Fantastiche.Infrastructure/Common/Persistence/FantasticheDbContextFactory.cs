using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Fantastiche.Infrastructure.Common.Persistence;

public sealed class FantasticheDbContextFactory : IDesignTimeDbContextFactory<FantasticheDbContext>
{
    public FantasticheDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Fantastiche")
         ?? throw new InvalidOperationException("Usa `just be migrate` per caricare la configurazione locale.");
        return new FantasticheDbContext(new DbContextOptionsBuilder<FantasticheDbContext>().UseSqlServer(connection).Options);
    }
}
