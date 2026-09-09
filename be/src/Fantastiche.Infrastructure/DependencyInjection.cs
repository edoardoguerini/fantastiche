using System.Security.Cryptography.X509Certificates;
using Fantastiche.Core.Email;
using Fantastiche.Gateways.Mailgun;
using Fantastiche.Infrastructure.Common;
using Fantastiche.Infrastructure.Common.Authentication;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Emails;
using Fantastiche.Infrastructure.Leagues;
using Fantastiche.Infrastructure.Catalog;
using Fantastiche.Infrastructure.Auctions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Fantastiche.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFantasticheInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Fantastiche")
         ?? throw new InvalidOperationException("Configura ConnectionStrings__Fantastiche prima di avviare il backend.");
        services.AddDbContext<FantasticheDbContext>(options => options.UseSqlServer(connectionString));
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.SignIn.RequireConfirmedEmail = true;
        }).AddEntityFrameworkStores<FantasticheDbContext>().AddDefaultTokenProviders();
        services.TryAddSingleton(TimeProvider.System);
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"] ?? "Production";
        var keyPath = configuration["DataProtection:KeyPath"];
        var certificatePath = configuration["DataProtection:CertificatePath"];
        if (environment != "Development" && (string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(certificatePath)))
            throw new InvalidOperationException("Fuori Development servono DataProtection__KeyPath e DataProtection__CertificatePath persistenti.");
        var protection = services.AddDataProtection().SetApplicationName("Fantastiche")
         .PersistKeysToFileSystem(new DirectoryInfo(keyPath ?? ".local/keys"));
        if (!string.IsNullOrWhiteSpace(certificatePath))
            protection.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12FromFile(certificatePath, configuration["DataProtection:CertificatePassword"]));
        services.AddScoped<EmailPayloadProtector>();
        services.AddScoped<LeagueWorkflow>();
        services.AddScoped<CatalogWorkflow>();
        services.AddScoped<AuctionEngine>();
        services.AddScoped<EmailDispatcher>();
        services.AddScoped<IRequestPublisher, RequestPublisher>();
        foreach (var type in typeof(DependencyInjection).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract))
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)))
                services.AddScoped(contract, type);
        var provider = configuration["Email:Provider"] ?? "Local";
        if (provider == "Mailgun")
        {
            if (!bool.TryParse(configuration["Email:EnableExternalDelivery"], out var enabled) || !enabled)
                throw new InvalidOperationException("L’invio Mailgun richiede Email__EnableExternalDelivery=true.");
            services.AddHttpClient<IEmailSender, MailgunEmailSender>(client => client.Timeout = TimeSpan.FromSeconds(30));
        }
        else if (provider == "Local" && environment == "Development") services.AddScoped<IEmailSender, LocalEmailSender>();
        else throw new InvalidOperationException("Configura un provider email valido; il mittente Local è disponibile solo in Development.");
        return services;
    }
}
