using Fantastiche.Core.Email;
using Fantastiche.Infrastructure.Common.Persistence;
using Fantastiche.Infrastructure.Emails;
using Fantastiche.Infrastructure.Leagues;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
namespace Fantastiche.IntegrationTests.Emails;

public sealed class EmailQueueTests : IAsyncLifetime
{
    private readonly SqlFixture fixture = new();
    public async Task InitializeAsync()
    {
        await fixture.InitializeAsync();
        var builder = new SqlConnectionStringBuilder(fixture.ConnectionString);
        var database = builder.InitialCatalog; builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString); await connection.OpenAsync();
        // Azure SQL usa RCSI: lo stesso regime è obbligatorio nel test di acquisizione.
        await using var cmd = connection.CreateCommand(); cmd.CommandText = $"ALTER DATABASE [{database}] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE";
        await cmd.ExecuteNonQueryAsync();
    }
    public Task DisposeAsync() => fixture.DisposeAsync();
    private async Task<Guid> Queue()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<LeagueWorkflow>().CreateLeagueAsync(new(fixture.Admin, "Lega", "2026", Guid.NewGuid() + "@example.test", "User"), default);
        return (await scope.ServiceProvider.GetRequiredService<FantasticheDbContext>().EmailMessages.SingleAsync()).Id;
    }
    private async Task<bool> Dispatch(IEmailSender sender)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var dispatcher = new EmailDispatcher(scope.ServiceProvider.GetRequiredService<FantasticheDbContext>(), sender,
         scope.ServiceProvider.GetRequiredService<EmailPayloadProtector>(), TimeProvider.System, NullLogger<EmailDispatcher>.Instance);
        return await dispatcher.DispatchOneAsync(default);
    }
    private async Task<EmailMessage> Message(Guid id)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<FantasticheDbContext>().EmailMessages.AsNoTracking().SingleAsync(x => x.Id == id);
    }
    [Fact]
    public async Task ConcurrentWorkersAcquireOnceWithAzureSqlSnapshotIsolation()
    {
        var id = await Queue(); var sender = new RecordingSender();
        var result = await Task.WhenAll(Dispatch(sender), Dispatch(sender));
        Assert.Single(result, x => x); Assert.Equal(1, sender.Deliveries);
        var row = await Message(id); Assert.Equal(EmailStatus.AcceptedByProvider, row.Status); Assert.Equal(1, row.Attempts);
        Assert.Equal("", row.ProtectedPayload); Assert.Null(row.LeaseId); Assert.Equal("test-provider", row.ProviderMessageId);
    }
    [Fact]
    public async Task TemporaryProviderFailureSchedulesRetryAndPreservesProtectedPayload()
    {
        var id = await Queue(); Assert.True(await Dispatch(new FailureSender(new HttpRequestException())));
        var row = await Message(id); Assert.Equal(EmailStatus.Pending, row.Status); Assert.Equal(1, row.Attempts);
        Assert.True(row.NextAttemptAt > DateTimeOffset.UtcNow); Assert.NotEmpty(row.ProtectedPayload); Assert.Null(row.LeaseId);
        Assert.False(await Dispatch(new RecordingSender()));
    }
    [Fact]
    public async Task PermanentFailureStopsRetriesAndRemovesSensitivePayload()
    {
        var id = await Queue(); await Dispatch(new FailureSender(new EmailDeliveryException(false)));
        var row = await Message(id); Assert.Equal(EmailStatus.Failed, row.Status); Assert.Empty(row.ProtectedPayload);
    }
    [Fact]
    public async Task RevokedInvitationIsCancelledWithoutDelivery()
    {
        var id = await Queue();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>();
            (await db.LeagueInvitations.SingleAsync()).RevokedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        }
        var sender = new RecordingSender(); Assert.False(await Dispatch(sender)); Assert.Equal(0, sender.Deliveries);
        var row = await Message(id); Assert.Equal(EmailStatus.Cancelled, row.Status); Assert.Empty(row.ProtectedPayload);
    }
    [Fact]
    public async Task ExpiredLeaseIsRecoveredAfterWorkerCrash()
    {
        var id = await Queue();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FantasticheDbContext>(); var row = await db.EmailMessages.SingleAsync();
            row.Status = EmailStatus.Processing; row.LeaseId = Guid.NewGuid(); row.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5); row.Attempts = 1; await db.SaveChangesAsync();
        }
        Assert.True(await Dispatch(new RecordingSender())); var stored = await Message(id);
        Assert.Equal(EmailStatus.AcceptedByProvider, stored.Status); Assert.Equal(2, stored.Attempts);
    }
    [Fact]
    public async Task DatabaseFailureAfterDeliveryPreservesLeaseAndPayloadForRecovery()
    {
        var id = await Queue();
        await Assert.ThrowsAsync<SqlException>(() => Dispatch(new PersistenceFailureSender(fixture.ConnectionString)));
        var row = await Message(id); Assert.Equal(EmailStatus.Processing, row.Status); Assert.NotEmpty(row.ProtectedPayload); Assert.NotNull(row.LeaseId);
    }
    private sealed class PersistenceFailureSender(string connectionString) : IEmailSender
    {
        public async Task<EmailSendResult> SendAsync(RenderedEmail email, CancellationToken ct = default)
        {
            await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TRIGGER SimulateDeliveryPersistenceFailure ON EmailMessages AFTER UPDATE AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE Status=2) THROW 51001, 'Simulated persistence failure', 1; END";
            await command.ExecuteNonQueryAsync(ct); return new("test-provider");
        }
    }
    private sealed class RecordingSender : IEmailSender
    {
        private int deliveries;
        public int Deliveries => deliveries;
        public async Task<EmailSendResult> SendAsync(RenderedEmail email, CancellationToken ct = default)
        {
            Assert.Contains("#token=", email.TextBody); Interlocked.Increment(ref deliveries);
            await Task.Delay(100, ct); return new("test-provider");
        }
    }
    private sealed class FailureSender(Exception error) : IEmailSender
    {
        public Task<EmailSendResult> SendAsync(RenderedEmail email, CancellationToken ct = default) => Task.FromException<EmailSendResult>(error);
    }
}
