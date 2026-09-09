using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fantastiche.Infrastructure.Common;

public static class DispatchServiceCollectionExtensions
{
    public static IServiceCollection AddFantasticheDispatch(this IServiceCollection services)
    {
        services.TryAddScoped<IRequestPublisher, RequestPublisher>();
        return services;
    }
}
