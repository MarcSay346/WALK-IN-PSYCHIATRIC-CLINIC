using System;

namespace ClinicApi.Services;

// ---------- Notification module ----------

public static class NotificationTypes
{
    public const string Queue = "queue";     // joined a queue, almost up
    public const string Status = "status";   // being served, left the queue
}

public class Notification
{
    public int Id { get; init; }
    public string UserId { get; init; } = "";
    public string Type { get; init; } = NotificationTypes.Queue;
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public bool Read { get; set; }
}

public class NotificationSettings
{
    public bool QueueUpdates { get; set; } = true;
    public bool StatusChanges { get; set; } = true;
    public int AlertWithinSpots { get; set; } = 3;
    public bool Popups { get; set; } = true;

    public NotificationSettings Copy() => (NotificationSettings)MemberwiseClone();
}

/// <summary>Body of PUT /api/notifications/{userId}/settings. Nullable so missing fields can be reported.</summary>
public record NotificationSettingsRequest(bool? QueueUpdates, bool? StatusChanges, int? AlertWithinSpots, bool? Popups);

// ---------- History module ----------

public static class HistoryOutcomes
{
    public const string Waiting = "Waiting";
    public const string Served = "Served";
    public const string Left = "Left";
}

public class HistoryEntry
{
    public int Id { get; init; }
    public string UserId { get; init; } = "";
    public string ServiceName { get; init; } = "";
    public DateTime JoinedAt { get; init; }
    public DateTime? EndedAt { get; set; }
    public string Outcome { get; set; } = HistoryOutcomes.Waiting;
}

public record HistorySummary(int TotalVisits, int Served, int Left, int Waiting);
