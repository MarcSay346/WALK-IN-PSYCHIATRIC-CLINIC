using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicApi.Services;

/// <summary>
/// History module (A3 module 6). Tracks each user's queue participation in memory:
/// joining a queue creates a "Waiting" entry, which later becomes "Served" or "Left".
/// </summary>
public class HistoryService
{
    public const int MaxUserIdLength = 100;
    public const int MaxServiceNameLength = 100;

    private readonly List<HistoryEntry> _entries = new();
    private readonly object _lock = new();
    private int _nextId = 1;

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
            if (FindWaiting(user, service) is not null)
                throw new InvalidOperationException($"You are already waiting in the queue for {service}.");

            var entry = new HistoryEntry
            {
                Id = _nextId++,
                UserId = user,
                ServiceName = service,
                JoinedAt = DateTime.UtcNow
            };
            _entries.Add(entry);
            return entry;
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
            var entry = FindWaiting(user, service)
                ?? throw new KeyNotFoundException($"No active queue visit found for {service}.");
            entry.Outcome = normalized!;
            entry.EndedAt = DateTime.UtcNow;
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
            return _entries
                .Where(e => e.UserId == user)
                .OrderByDescending(e => e.Id)
                .ToList();
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

    private HistoryEntry? FindWaiting(string user, string service) =>
        _entries.LastOrDefault(e =>
            e.UserId == user &&
            e.Outcome == HistoryOutcomes.Waiting &&
            string.Equals(e.ServiceName, service, StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeOutcome(string? outcome) =>
        outcome?.Trim().ToLowerInvariant() switch
        {
            "served" => HistoryOutcomes.Served,
            "left" => HistoryOutcomes.Left,
            _ => null
        };
}
