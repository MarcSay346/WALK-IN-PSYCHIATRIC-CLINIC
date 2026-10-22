using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicApi.Services;

/// <summary>
/// Notification module (A3 module 5). Stores notifications in memory and creates them when:
///   1. a user joins a queue            -> NotifyJoined
///   2. a user is close to being served -> CheckCloseToServed
/// plus status changes (being served / leaving). Notifications are logged to the console
/// and returned to the front end through the API (no real email/SMS).
/// </summary>
public class NotificationService
{
    public const int MaxUserIdLength = 100;
    public const int MaxServiceNameLength = 100;
    public const int MaxTitleLength = 100;
    public const int MaxMessageLength = 300;
    public const int MinAlertSpots = 1;
    public const int MaxAlertSpots = 10;
    public const int MaxQueuePosition = 1000;
    public const int MaxWaitMinutes = 1440;

    private readonly List<Notification> _notifications = new();
    private readonly Dictionary<string, NotificationSettings> _settings = new();
    private readonly Dictionary<string, int> _lastAlertedPosition = new();   // "user|service" -> position
    private readonly object _lock = new();
    private int _nextId = 1;

    // ---------- trigger 1: user joins a queue ----------

    public Notification? NotifyJoined(string? userId, string? serviceName, int? position, int? estimatedWaitMinutes)
    {
        ValidateQueueEvent(userId, serviceName, position, estimatedWaitMinutes, requireWaitTime: true);
        var user = userId!.Trim();
        var service = serviceName!.Trim();

        lock (_lock)
        {
            // a fresh visit, so "almost up" reminders can fire again
            _lastAlertedPosition.Remove(PositionKey(user, service));

            if (!SettingsFor(user).QueueUpdates) return null;

            return Add(user, NotificationTypes.Queue,
                $"You joined the queue for {service}",
                $"You are #{position} in line. Estimated wait: {FormatWait(estimatedWaitMinutes!.Value)}.");
        }
    }

    // ---------- trigger 2: user is close to being served ----------

    public Notification? CheckCloseToServed(string? userId, string? serviceName, int? position)
    {
        ValidateQueueEvent(userId, serviceName, position, null, requireWaitTime: false);
        var user = userId!.Trim();
        var service = serviceName!.Trim();
        var spot = position!.Value;

        lock (_lock)
        {
            var settings = SettingsFor(user);
            if (!settings.QueueUpdates || spot > settings.AlertWithinSpots) return null;

            // don't repeat the same alert if the position hasn't changed
            var key = PositionKey(user, service);
            if (_lastAlertedPosition.TryGetValue(key, out var last) && last == spot) return null;
            _lastAlertedPosition[key] = spot;

            if (spot == 1)
            {
                return Add(user, NotificationTypes.Queue, "You're next in line",
                    $"A provider for {service} will be ready for you shortly. Please stay nearby.");
            }

            var ahead = spot - 1;
            return Add(user, NotificationTypes.Queue, $"You're almost up: #{spot} in line",
                $"Only {ahead} {(ahead == 1 ? "person is" : "people are")} ahead of you for {service}.");
        }
    }

    // ---------- status changes (served / left) ----------

    public Notification? NotifyStatusChange(string? userId, string? title, string? message)
    {
        var errors = new Dictionary<string, string>();
        InputRules.RequiredText(errors, "userId", "User ID", userId, MaxUserIdLength);
        InputRules.RequiredText(errors, "title", "Title", title, MaxTitleLength);
        InputRules.RequiredText(errors, "message", "Message", message, MaxMessageLength);
        InputRules.ThrowIfInvalid(errors);

        var user = userId!.Trim();
        lock (_lock)
        {
            if (!SettingsFor(user).StatusChanges) return null;
            return Add(user, NotificationTypes.Status, title!.Trim(), message!.Trim());
        }
    }

    // ---------- reading and updating ----------

    public List<Notification> GetForUser(string? userId, string? type = null)
    {
        var user = RequireUserId(userId);
        var filter = string.IsNullOrWhiteSpace(type) ? "all" : type.Trim().ToLowerInvariant();
        if (filter != "all" && filter != NotificationTypes.Queue && filter != NotificationTypes.Status)
            throw new ApiValidationException("type", "Type must be all, queue, or status.");

        lock (_lock)
        {
            return _notifications
                .Where(n => n.UserId == user && (filter == "all" || n.Type == filter))
                .OrderByDescending(n => n.Id)
                .ToList();
        }
    }

    public int UnreadCount(string? userId)
    {
        var user = RequireUserId(userId);
        lock (_lock)
        {
            return _notifications.Count(n => n.UserId == user && !n.Read);
        }
    }

    public bool MarkRead(string? userId, int notificationId)
    {
        var user = RequireUserId(userId);
        lock (_lock)
        {
            var note = _notifications.FirstOrDefault(n => n.Id == notificationId && n.UserId == user);
            if (note is null) return false;
            note.Read = true;
            return true;
        }
    }

    public int MarkAllRead(string? userId)
    {
        var user = RequireUserId(userId);
        lock (_lock)
        {
            var unread = _notifications.Where(n => n.UserId == user && !n.Read).ToList();
            unread.ForEach(n => n.Read = true);
            return unread.Count;
        }
    }

    public int ClearForUser(string? userId)
    {
        var user = RequireUserId(userId);
        lock (_lock)
        {
            foreach (var key in _lastAlertedPosition.Keys.Where(k => k.StartsWith(user + "|")).ToList())
                _lastAlertedPosition.Remove(key);
            return _notifications.RemoveAll(n => n.UserId == user);
        }
    }

    // ---------- settings ----------

    public NotificationSettings GetSettings(string? userId)
    {
        var user = RequireUserId(userId);
        lock (_lock)
        {
            return SettingsFor(user).Copy();
        }
    }

    public NotificationSettings UpdateSettings(string? userId, NotificationSettingsRequest? request)
    {
        var errors = new Dictionary<string, string>();
        InputRules.RequiredText(errors, "userId", "User ID", userId, MaxUserIdLength);

        if (request is null)
        {
            errors["body"] = "Settings are required.";
            throw new ApiValidationException(errors);
        }

        if (request.QueueUpdates is null) errors["queueUpdates"] = "Choose whether to get queue updates.";
        if (request.StatusChanges is null) errors["statusChanges"] = "Choose whether to get status changes.";
        if (request.QueueUpdates == false && request.StatusChanges == false)
            errors["types"] = "Choose at least one type of notification.";
        InputRules.IntInRange(errors, "alertWithinSpots", "Alert range", request.AlertWithinSpots, MinAlertSpots, MaxAlertSpots);
        InputRules.ThrowIfInvalid(errors);

        var user = userId!.Trim();
        lock (_lock)
        {
            var current = SettingsFor(user);
            var updated = new NotificationSettings
            {
                QueueUpdates = request.QueueUpdates!.Value,
                StatusChanges = request.StatusChanges!.Value,
                AlertWithinSpots = request.AlertWithinSpots!.Value,
                Popups = request.Popups ?? current.Popups
            };
            _settings[user] = updated;
            return updated.Copy();
        }
    }

    // ---------- helpers ----------

    public static void ValidateQueueEvent(string? userId, string? serviceName, int? position, int? estimatedWaitMinutes, bool requireWaitTime)
    {
        var errors = new Dictionary<string, string>();
        InputRules.RequiredText(errors, "userId", "User ID", userId, MaxUserIdLength);
        InputRules.RequiredText(errors, "serviceName", "Service name", serviceName, MaxServiceNameLength);
        InputRules.IntInRange(errors, "position", "Queue position", position, 1, MaxQueuePosition);
        if (requireWaitTime)
            InputRules.IntInRange(errors, "estimatedWaitMinutes", "Estimated wait", estimatedWaitMinutes, 0, MaxWaitMinutes);
        InputRules.ThrowIfInvalid(errors);
    }

    public static string FormatWait(int minutes)
    {
        if (minutes <= 0) return "less than a minute";
        if (minutes == 1) return "1 minute";
        if (minutes < 60) return $"{minutes} minutes";

        var hours = minutes / 60;
        var rest = minutes % 60;
        var hourText = hours == 1 ? "1 hour" : $"{hours} hours";
        return rest == 0 ? hourText : $"{hourText} {rest} min";
    }

    private static string RequireUserId(string? userId)
    {
        var errors = new Dictionary<string, string>();
        InputRules.RequiredText(errors, "userId", "User ID", userId, MaxUserIdLength);
        InputRules.ThrowIfInvalid(errors);
        return userId!.Trim();
    }

    private NotificationSettings SettingsFor(string user) =>
        _settings.TryGetValue(user, out var saved) ? saved : new NotificationSettings();

    private static string PositionKey(string user, string service) => $"{user}|{service.ToLowerInvariant()}";

    private Notification Add(string user, string type, string title, string message)
    {
        var note = new Notification
        {
            Id = _nextId++,
            UserId = user,
            Type = type,
            Title = title,
            Message = message,
            CreatedAt = DateTime.UtcNow
        };
        _notifications.Add(note);
        Console.WriteLine($"[Notification] to {user}: {title}");
        return note;
    }
}
