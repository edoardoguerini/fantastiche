using System.Reflection;

namespace Fantastiche.Application.Infrastructure.Modules;

public static class ModuleExtensions
{
    public static IServiceCollection AddFantasticheModules(
        this IServiceCollection services,
        Assembly assembly)
    {
        var moduleTypes = assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => typeof(IRegistrableModule).IsAssignableFrom(type));

        foreach (var moduleType in moduleTypes)
        {
            services.AddSingleton(typeof(IRegistrableModule), moduleType);
        }

        return services;
    }

    public static WebApplication MapFantasticheModules(this WebApplication app)
    {
        // ExplicitAntiforgeryMiddleware protegge tutte le mutazioni sotto questo gruppo.
        var api = app.MapGroup("/api");
        foreach (var module in app.Services.GetServices<IRegistrableModule>())
        {
            module.RegisterEndpoints(api);
        }

        return app;
    }
}
