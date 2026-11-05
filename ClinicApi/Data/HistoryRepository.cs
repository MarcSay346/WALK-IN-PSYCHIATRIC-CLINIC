using System;
using System.Collections.Generic;
using System.Linq;
using ClinicApi.Services;
using Dapper;

namespace ClinicApi.Data;

/// <summary>Where each user's queue visits (history) are stored.</summary>
public interface IHistoryRepository
{
    HistoryEntry Add(string userId, string serviceName, DateTime joinedAt);

    /// <summary>The user's latest still-waiting visit for a service (service name ignores case), or null.</summary>
    HistoryEntry? FindWaiting(string userId, string serviceName);

    void Complete(HistoryEntry entry, string outcome, DateTime endedAt);

    /// <summary>Newest first.</summary>
    List<HistoryEntry> GetForUser(string userId);
}

/// <summary>A3 behaviour: everything in memory (used by the unit tests and when no database is set).</summary>
public class InMemoryHistoryRepository : IHistoryRepository
{
    private readonly List<HistoryEntry> _entries = new();
    private int _nextId = 1;

    public HistoryEntry Add(string userId, string serviceName, DateTime joinedAt)
    {
        var entry = new HistoryEntry { Id = _nextId++, UserId = userId, ServiceName = serviceName, JoinedAt = joinedAt };
        _entries.Add(entry);
        return entry;
    }

    public HistoryEntry? FindWaiting(string userId, string serviceName) =>
        _entries.LastOrDefault(e =>
            e.UserId == userId &&
            e.Outcome == HistoryOutcomes.Waiting &&
            string.Equals(e.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase));

    public void Complete(HistoryEntry entry, string outcome, DateTime endedAt)
    {
        entry.Outcome = outcome;
        entry.EndedAt = endedAt;
    }

    public List<HistoryEntry> GetForUser(string userId) =>
        _entries.Where(e => e.UserId == userId).OrderByDescending(e => e.Id).ToList();
}

/// <summary>A4: VisitHistory table.</summary>
public class SqlHistoryRepository : IHistoryRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlHistoryRepository(IDbConnectionFactory factory) => _factory = factory;

    private class HistoryRow
    {
        public int Id { get; set; }
        public string ServiceName { get; set; } = "";
        public DateTime JoinedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public string Outcome { get; set; } = HistoryOutcomes.Waiting;
    }

    private static HistoryEntry ToModel(HistoryRow row, string userId) => new()
    {
        Id = row.Id,
        UserId = userId,
        ServiceName = row.ServiceName,
        JoinedAt = row.JoinedAt,
        EndedAt = row.EndedAt,
        Outcome = row.Outcome
    };

    private const string SelectColumns =
        "SELECT HistoryId AS Id, ServiceName, JoinedAt, EndedAt, Outcome FROM VisitHistory ";

    public HistoryEntry Add(string userId, string serviceName, DateTime joinedAt)
    {
        using var db = _factory.Open();
        var dbUserId = DbUsers.Resolve(db, userId);
        var id = db.ExecuteScalar<int>(
            @"INSERT INTO VisitHistory (UserId, ServiceName, JoinedAt, Outcome)
              VALUES (@dbUserId, @serviceName, @joinedAt, 'Waiting');" + _factory.SelectLastId,
            new { dbUserId, serviceName, joinedAt });
        return new HistoryEntry { Id = id, UserId = userId, ServiceName = serviceName, JoinedAt = joinedAt };
    }

    public HistoryEntry? FindWaiting(string userId, string serviceName)
    {
        using var db = _factory.Open();
        var dbUserId = DbUsers.TryResolve(db, userId);
        if (dbUserId is null) return null;

        var row = db.Query<HistoryRow>(
                SelectColumns + @"WHERE UserId = @dbUserId AND Outcome = 'Waiting'
                                  AND LOWER(ServiceName) = LOWER(@serviceName)
                                  ORDER BY HistoryId DESC",
                new { dbUserId, serviceName })
            .FirstOrDefault();
        return row is null ? null : ToModel(row, userId);
    }

    public void Complete(HistoryEntry entry, string outcome, DateTime endedAt)
    {
        using var db = _factory.Open();
        db.Execute("UPDATE VisitHistory SET Outcome = @outcome, EndedAt = @endedAt WHERE HistoryId = @Id",
            new { outcome, endedAt, entry.Id });
        entry.Outcome = outcome;
        entry.EndedAt = endedAt;
    }

    public List<HistoryEntry> GetForUser(string userId)
    {
        using var db = _factory.Open();
        var dbUserId = DbUsers.TryResolve(db, userId);
        if (dbUserId is null) return new List<HistoryEntry>();

        return db.Query<HistoryRow>(SelectColumns + "WHERE UserId = @dbUserId ORDER BY HistoryId DESC", new { dbUserId })
                 .Select(r => ToModel(r, userId))
                 .ToList();
    }
}
