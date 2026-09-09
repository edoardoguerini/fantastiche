using Fantastiche.Infrastructure.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Fantastiche.UnitTests.Common.Dispatch;

public sealed class RequestPublisherTests
{
    [Fact]
    public async Task SendAsync_RisolveHandlerEPropagaCancellationToken()
    {
        var services = new ServiceCollection();
        var handler = new EchoRequestHandler();
        services.AddScoped<IRequestHandler<EchoRequest, string>>(_ => handler);
        services.AddFantasticheDispatch();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();

        var result = await scope.ServiceProvider
            .GetRequiredService<IRequestPublisher>()
            .SendAsync<EchoRequest, string>(new EchoRequest("fantastiche"), cancellation.Token);

        Assert.Equal("fantastiche", result);
        Assert.Equal(cancellation.Token, handler.CancellationToken);
    }

    private sealed record EchoRequest(string Value) : IRequest<string>;

    private sealed class EchoRequestHandler : IRequestHandler<EchoRequest, string>
    {
        public CancellationToken CancellationToken { get; private set; }

        public Task<string> HandleAsync(EchoRequest request, CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return Task.FromResult(request.Value);
        }
    }
}
