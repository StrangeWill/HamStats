using System.Collections.Concurrent;
using RoushTech.Asio;

namespace HamStats.Website.Logging;

/// <summary>
/// In-process <see cref="IJobSessionStore"/> for Asio job-session logs. The shipped store is
/// Redis-backed; HamStats is a single-container, Redis-free app, and a session only needs to outlive
/// one job run (long enough for a client to replay it), so an in-memory store is sufficient.
/// Sessions live until eviction once <see cref="MaxSessions"/> is exceeded (oldest first).
/// </summary>
public class InMemoryJobSessionStore : IJobSessionStore
{
    private const int MaxSessions = 50;

    private sealed class Entry
    {
        public required string Label { get; init; }
        public string? OwnerName { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? CompletedAt { get; set; }
        public bool IsComplete { get; set; }
        public bool HasError { get; set; }
        public List<LogEntry> Logs { get; } = new();
    }

    private readonly ConcurrentDictionary<Guid, Entry> _sessions = new();

    public Task CreateSession(Guid sessionId, string label, string? ownerName)
    {
        _sessions[sessionId] = new Entry
        {
            Label = label,
            OwnerName = ownerName,
            CreatedAt = DateTime.UtcNow,
        };
        Evict();
        return Task.CompletedTask;
    }

    public Task AppendLog(Guid sessionId, string message, int level)
    {
        if (_sessions.TryGetValue(sessionId, out var entry))
        {
            lock (entry.Logs)
            {
                entry.Logs.Add(new LogEntry(message, level));
            }
        }

        return Task.CompletedTask;
    }

    public Task Complete(Guid sessionId, bool hasError)
    {
        if (_sessions.TryGetValue(sessionId, out var entry))
        {
            entry.IsComplete = true;
            entry.HasError = hasError;
            entry.CompletedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<SessionSnapshot> GetSession(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            return Task.FromResult<SessionSnapshot>(null!);
        }

        IReadOnlyList<LogEntry> logs;
        lock (entry.Logs)
        {
            logs = entry.Logs.ToList();
        }

        return Task.FromResult(new SessionSnapshot(
            logs, entry.IsComplete, entry.HasError, entry.Label, entry.CreatedAt, entry.CompletedAt));
    }

    public Task<IReadOnlyList<SessionInfo>> GetSessions(IReadOnlyList<Guid> sessionIds)
    {
        var infos = sessionIds
            .Where(_sessions.ContainsKey)
            .Select(id =>
            {
                var e = _sessions[id];
                return new SessionInfo(id, e.Label, e.CreatedAt, e.CompletedAt, e.IsComplete, e.HasError, e.OwnerName);
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<SessionInfo>>(infos);
    }

    private void Evict()
    {
        while (_sessions.Count > MaxSessions)
        {
            var oldest = _sessions.OrderBy(kvp => kvp.Value.CreatedAt).FirstOrDefault();
            if (oldest.Key == Guid.Empty && !_sessions.ContainsKey(Guid.Empty))
            {
                break;
            }

            _sessions.TryRemove(oldest.Key, out _);
        }
    }
}
