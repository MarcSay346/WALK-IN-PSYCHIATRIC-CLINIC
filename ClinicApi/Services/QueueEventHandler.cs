using System.Collections.Generic;

namespace ClinicApi.Services;

public record QueueJoinedResult(HistoryEntry HistoryEntry, List<Notification> Notifications);

public record QueueVisitResult(HistoryEntry HistoryEntry, Notification? Notification);

/// <summary>
/// Connects the Queue module to the Notification and History modules.
/// The queue code calls these methods when something happens to a user in a queue:
///   join       -> OnUserJoined       (history entry + "joined" notification + "almost up" if near the front)
///   move up    -> OnPositionChanged  ("almost up" notification when within the user's alert range)
///   serve next -> OnUserServed       (history = Served + status notification)
///   leave      -> OnUserLeft         (history = Left + status notification)
/// </summary>
public class QueueEventHandler
{
    private readonly NotificationService _notifications;
    private readonly HistoryService _history;

    public QueueEventHandler(NotificationService notifications, HistoryService history)
    {
        _notifications = notifications;
        _history = history;
    }

    public QueueJoinedResult OnUserJoined(string? userId, string? serviceName, int? position, int? estimatedWaitMinutes)
    {
        // validate everything first so nothing is half-saved
        NotificationService.ValidateQueueEvent(userId, serviceName, position, estimatedWaitMinutes, requireWaitTime: true);

        var entry = _history.RecordJoin(userId, serviceName);

        var sent = new List<Notification>();
        var joined = _notifications.NotifyJoined(userId, serviceName, position, estimatedWaitMinutes);
        if (joined is not null) sent.Add(joined);

        var almostUp = _notifications.CheckCloseToServed(userId, serviceName, position);
        if (almostUp is not null) sent.Add(almostUp);

        return new QueueJoinedResult(entry, sent);
    }

    public Notification? OnPositionChanged(string? userId, string? serviceName, int? position) =>
        _notifications.CheckCloseToServed(userId, serviceName, position);

    public QueueVisitResult OnUserServed(string? userId, string? serviceName)
    {
        var entry = _history.CompleteLatest(userId, serviceName, HistoryOutcomes.Served);
        var note = _notifications.NotifyStatusChange(entry.UserId, "It's your turn",
            $"You are now being served for {entry.ServiceName}. Please head to the front desk.");
        return new QueueVisitResult(entry, note);
    }

    public QueueVisitResult OnUserLeft(string? userId, string? serviceName)
    {
        var entry = _history.CompleteLatest(userId, serviceName, HistoryOutcomes.Left);
        var note = _notifications.NotifyStatusChange(entry.UserId, "You left the queue",
            $"You left the queue for {entry.ServiceName}.");
        return new QueueVisitResult(entry, note);
    }
}
