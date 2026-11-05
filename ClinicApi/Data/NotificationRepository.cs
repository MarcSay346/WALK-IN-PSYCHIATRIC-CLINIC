using System;
using System.Collections.Generic;
using System.Linq;
using ClinicApi.Services;
using Dapper;

namespace ClinicApi.Data;

/// <summary>Where notifications and notification settings are stored.</summary>
public interface INotificationRepository
{
    Notification Add(string userId, string type, string title, string message, DateTime createdAt);

    /// <summary>Newest first. <paramref name="type"/> null = all types.</summary>
    List<Notification> GetForUser(string userId, string? type);

    int CountUnread(string userId);
    bool MarkRead(string userId, int notificationId);
    int MarkAllRead(string userId);
    int DeleteForUser(string userId);

    /// <summary>Saved settings, or null if the user never changed them.</summary>
    NotificationSettings? GetSettings(string userId);
    void SaveSettings(string userId, NotificationSettings settings);
}

/// <summary>A3 behaviour: everything in memory (used by the unit tests and when no database is set).</summary>
public class InMemoryNotificationRepository : INotificationRepository
{
    private readonly List<Notification> _notifications = new();
    private readonly Dictionary<string, NotificationSettings> _settings = new();
    private int _nextId = 1;

    public Notification Add(string userId, string type, string title, string message, DateTime createdAt)
    {
        var note = new Notification
        {
            Id = _nextId++, UserId = userId, Type = type, Title = title, Message = message, CreatedAt = createdAt
        };
        _notifications.Add(note);
        return note;
    }

    public List<Notification> GetForUser(string userId, string? type) =>
        _notifications
            .Where(n => n.UserId == userId && (type is null || n.Type == type))
            .OrderByDescending(n => n.Id)
            .ToList();

    public int CountUnread(string userId) => _notifications.Count(n => n.UserId == userId && !n.Read);

    public bool MarkRead(string userId, int notificationId)
    {
        var note = _notifications.FirstOrDefault(n => n.Id == notificationId && n.UserId == userId);
        if (note is null) return false;
        note.Read = true;
        return true;
    }

    public int MarkAllRead(string userId)
    {
        var unread = _notifications.Where(n => n.UserId == userId && !n.Read).ToList();
        unread.ForEach(n => n.Read = true);
        return unread.Count;
    }

    public int DeleteForUser(string userId) => _notifications.RemoveAll(n => n.UserId == userId);

    public NotificationSettings? GetSettings(string userId) =>
        _settings.TryGetValue(userId, out var saved) ? saved.Copy() : null;

    public void SaveSettings(string userId, NotificationSettings settings) => _settings[userId] = settings.Copy();
}

/// <summary>A4: Notification table + the preference columns of UserProfile.</summary>
public class SqlNotificationRepository : INotificationRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlNotificationRepository(IDbConnectionFactory factory) => _factory = factory;

    private class NotificationRow
    {
        public int Id { get; set; }
        public string Type { get; set; } = "";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Status { get; set; } = "Sent";
        public DateTime CreatedAt { get; set; }
    }

    private static Notification ToModel(NotificationRow row, string userId) => new()
    {
        Id = row.Id,
        UserId = userId,
        Type = row.Type,
        Title = row.Title,
        Message = row.Message,
        CreatedAt = row.CreatedAt,
        Read = row.Status == "Viewed"
    };

    public Notification Add(string userId, string type, string title, string message, DateTime createdAt)
    {
        using var db = _factory.Open();
        var dbUserId = DbUsers.Resolve(db, userId);
        var id = db.ExecuteScalar<int>(
            @"INSERT INTO Notification (UserId, Type, Title, Message, Status, CreatedAt)
              VALUES (@dbUserId, @type, @title, @message, 'Sent', @createdAt);" + _factory.SelectLastId,
            new { dbUserId, type, title, message, createdAt });

        return new Notification
        {
            Id = id, UserId = userId, Type = type, Title = title, Message = message, CreatedAt = createdAt
        };
    }

    public List<Notification> GetForUser(string userId, string? type)
    {
        using var db = _factory.Open();
        var dbUserId = DbUsers.TryResolve(db, userId);
        if (dbUserId is null) return new List<Notification>();

        var sql = @"SELECT NotificationId AS Id, Type, Title, Message, Status, CreatedAt
                    FROM Notification WHERE UserId = @dbUserId"
                  + (type is null ? "" : " AND Type = @type")
                  + " ORDER BY NotificationId DESC";
        return db.Query<NotificationRow>(sql, new { dbUserId, type })
                 .Select(r => ToModel(r, userId))
                 .ToList();
    }

    public int CountUnread(string userId)
    {
        using var db = _factory.Open();
        var dbUserId = DbUsers.TryResolve(db, userId);
        if (dbUserId is null) return 0;
        return db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Notification WHERE UserId = @dbUserId AND Status = 'Sent'", new { dbUserId });
    }

    public bool MarkRead(string userId, int notificationId)
    {
        using var db = _factory.Open();
        var dbUserId = DbUsers.TryResolve(db, userId);
        if (dbUserId is null) return false;
        return db.Execute(
            "UPDATE Notification SET Status = 'Viewed' WHERE NotificationId = @notificationId AND UserId = @dbUserId",
            new { notificationId, dbUserId }) > 0;
    }

    public int MarkAllRead(string userId)
    {
        using var db = _factory.Open();
        var dbUserId = DbUsers.TryResolve(db, userId);
        if (dbUserId is null) return 0;
        return db.Execute(
            "UPDATE Notification SET Status = 'Viewed' WHERE UserId = @dbUserId AND Status = 'Sent'", new { dbUserId });
    }

    public int DeleteForUser(string userId)
    {
        using var db = _factory.Open();
        var dbUserId = DbUsers.TryResolve(db, userId);
        if (dbUserId is null) return 0;
        return db.Execute("DELETE FROM Notification WHERE UserId = @dbUserId", new { dbUserId });
    }

    public NotificationSettings? GetSettings(string userId)
    {
        using var db = _factory.Open();
        var dbUserId = DbUsers.TryResolve(db, userId);
        if (dbUserId is null) return null;
        return db.QuerySingleOrDefault<NotificationSettings>(
            "SELECT QueueUpdates, StatusChanges, AlertWithinSpots, Popups FROM UserProfile WHERE UserId = @dbUserId",
            new { dbUserId });
    }

    public void SaveSettings(string userId, NotificationSettings settings)
    {
        using var db = _factory.Open();
        var dbUserId = DbUsers.Resolve(db, userId);
        var updated = db.Execute(
            @"UPDATE UserProfile
              SET QueueUpdates = @QueueUpdates, StatusChanges = @StatusChanges,
                  AlertWithinSpots = @AlertWithinSpots, Popups = @Popups
              WHERE UserId = @dbUserId",
            new { settings.QueueUpdates, settings.StatusChanges, settings.AlertWithinSpots, settings.Popups, dbUserId });
        if (updated == 0)
            throw new KeyNotFoundException($"No profile found for user '{userId}'.");
    }
}
