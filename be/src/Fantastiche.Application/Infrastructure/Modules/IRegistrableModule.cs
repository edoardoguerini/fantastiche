namespace Fantastiche.Application.Infrastructure.Modules;

public interface IRegistrableModule
{
    void RegisterEndpoints(IEndpointRouteBuilder api);
}
