using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.Infrastructure.Common;

public sealed class RequestPublisher(IServiceProvider serviceProvider) : IRequestPublisher
{
    public Task<TResponse> SendAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>
        => DispatchAsync<TRequest, TResponse>(request, cancellationToken);

    public Task<TResponse> QueryAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IRequest<TResponse>
        => DispatchAsync<TRequest, TResponse>(request, cancellationToken);

    private Task<TResponse> DispatchAsync<TRequest, TResponse>(
        TRequest request,
        CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        return handler.HandleAsync(request, cancellationToken);
    }
}
