using System;
using System.Collections.Generic;
using System.Linq;
using ClinicApi.Data;

namespace ClinicApi.Services;

/// <summary>
/// History module (A3 module 6). Tracks each user's queue participation in memory:
/// joining a queue creates a "Waiting" entry, which later becomes "Served" or "Left".
/// A4: entries are stored through an IHistoryRepository (VisitHistory table in SQL Server,
/// in memory in the unit tests).
/// </summary>
public class HistoryService
{
    public const int MaxUserIdLength = 100;
    public const int MaxServiceNameLength = 100;

    private readonly IHistoryRepository _store;
    private readonly object _lock = new();

    public HistoryService() : this(new InMemoryHistoryRepository()) { }

    public HistoryService(IHistoryRepository store) => _store = store;

    public HistoryEntry RecordJoin(string? userId, string? serviceName)
    {
        var errors = new Dictionary<string, string>();
        InputRules.RequiredText(errors, "userId", "User ID", userId, MaxUserIdLength);
        InputRules.RequiredText(errors, "serviceName", "Service name", serviceName, MaxServiceNameLength);
        InputRules.ThrowIfInvalid(errors);

        var user = userId!.Trim();
        var service = serviceName!.Trim();

        lock (_lock)
        {
            if (_store.FindWaiting(user, service) is not null)
                throw new InvalidOperationException($"You are already waiting in the queue for {service}.");

            return _store.Add(user, service, DateTime.UtcNow);
        }
    }

    /// <summary>Closes the user's open visit for a service with the outcome "Served" or "Left".</summary>
    public HistoryEntry CompleteLatest(string? userId, string? serviceName, string? outcome)
    {
        var errors = new Dictionary<string, string>();
        InputRules.RequiredText(errors, "userId", "User ID", userId, MaxUserIdLength);
        InputRules.RequiredText(errors, "serviceName", "Service name", serviceName, MaxServiceNameLength);
        var normalized = NormalizeOutcome(outcome);
        if (normalized is null)
            errors["outcome"] = "Outcome must be Served or Left.";
        InputRules.ThrowIfInvalid(errors);

        var user = userId!.Trim();
        var service = serviceName!.Trim();

        lock (_lock)
        {
            var entry = _store.FindWaiting(user, service)
                ?? throw new KeyNotFoundException($"No active queue visit found for {service}.");
            _store.Complete(entry, normalized!, DateTime.UtcNow);
            return entry;
        }
    }

    public List<HistoryEntry> GetForUser(string? userId)
    {
        var errors = new Dictionary<string, string>();
        InputRules.RequiredText(errors, "userId", "User ID", userId, MaxUserIdLength);
        InputRules.ThrowIfInvalid(errors);
        var user = userId!.Trim();

        lock (_lock)
        {
            return _store.GetForUser(user);
        }
    }

    public HistorySummary GetSummary(string? userId)
    {
        var entries = GetForUser(userId);
        return new HistorySummary(
            TotalVisits: entries.Count,
            Served: entries.Count(e => e.Outcome == HistoryOutcomes.Served),
            Left: entries.Count(e => e.Outcome == HistoryOutcomes.Left),
            Waiting: entries.Count(e => e.Outcome == HistoryOutcomes.Waiting));
    }

    private static string? NormalizeOutcome(string? outcome) =>
        outcome?.Trim().ToLowerInvariant() switch
        {
            "served" => HistoryOutcomes.Served,
            "left" => HistoryOutcomes.Left,
            _ => null
        };
}
