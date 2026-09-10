using System.Collections.Concurrent;

namespace Fantastiche.Application.Infrastructure.Auctions;

public sealed record AuctionObserver(
    string ConnectionId,
    Guid UserId,
    Guid SessionId,
    long KnownVersion);

public sealed class AuctionSubscriptionRegistry
{
    public const int MaxSessionsPerConnection = 16;

    private readonly ConcurrentDictionary<string, ConnectionSubscriptions> connections =
        new(StringComparer.Ordinal);

    public bool TryWatch(
        string connectionId,
        Guid userId,
        Guid sessionId,
        long knownVersion)
    {
        var subscriptions = connections.GetOrAdd(
            connectionId,
            _ => new ConnectionSubscriptions(userId));

        lock (subscriptions.SyncRoot)
        {
            if (subscriptions.UserId != userId)
            {
                return false;
            }

            if (subscriptions.Sessions.TryGetValue(sessionId, out var currentVersion))
            {
                subscriptions.Sessions[sessionId] = Math.Max(currentVersion, knownVersion);
                return true;
            }

            if (subscriptions.Sessions.Count >= MaxSessionsPerConnection)
            {
                return false;
            }

            subscriptions.Sessions.Add(sessionId, knownVersion);
            return true;
        }
    }

    public void Unwatch(string connectionId, Guid sessionId)
    {
        if (!connections.TryGetValue(connectionId, out var subscriptions))
        {
            return;
        }

        lock (subscriptions.SyncRoot)
        {
            subscriptions.Sessions.Remove(sessionId);
        }
    }

    public void RemoveConnection(string connectionId)
        => connections.TryRemove(connectionId, out _);

    public int CountConnectedUsers(Guid sessionId)
        => GetObservers()
            .Where(observer => observer.SessionId == sessionId)
            .Select(observer => observer.UserId)
            .Distinct()
            .Count();

    public IReadOnlyList<AuctionObserver> GetObservers()
    {
        var observers = new List<AuctionObserver>();
        foreach (var (connectionId, subscriptions) in connections)
        {
            lock (subscriptions.SyncRoot)
            {
                observers.AddRange(subscriptions.Sessions.Select(session =>
                    new AuctionObserver(
                        connectionId,
                        subscriptions.UserId,
                        session.Key,
                        session.Value)));
            }
        }

        return observers;
    }

    public bool TryAdvanceVersion(string connectionId, Guid sessionId, long version)
    {
        if (!connections.TryGetValue(connectionId, out var subscriptions))
        {
            return false;
        }

        lock (subscriptions.SyncRoot)
        {
            if (!subscriptions.Sessions.TryGetValue(sessionId, out var knownVersion) ||
                version <= knownVersion)
            {
                return false;
            }

            subscriptions.Sessions[sessionId] = version;
            return true;
        }
    }

    public bool NeedsNotification(string connectionId, Guid sessionId, long version)
    {
        if (!connections.TryGetValue(connectionId, out var subscriptions))
        {
            return false;
        }

        lock (subscriptions.SyncRoot)
        {
            return subscriptions.Sessions.TryGetValue(sessionId, out var knownVersion) &&
                   version > knownVersion;
        }
    }

    private sealed class ConnectionSubscriptions(Guid userId)
    {
        public object SyncRoot { get; } = new();
        public Guid UserId { get; } = userId;
        public Dictionary<Guid, long> Sessions { get; } = [];
    }
}
