using System;
using System.Collections.Generic;
using ClinicApi.Data;
using ClinicApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicApi.Tests;

/// <summary>A4: Notification + History modules stored in the database (SQLite test copy of the schema).</summary>
public class SqlNotificationHistoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly NotificationService _notifications;
    private readonly HistoryService _history;
    private readonly QueueEventHandler _events;
    private readonly int _userId;
    private const string Email = "patient@test.com";
    private const string Service = "Dr. Cole";

    public SqlNotificationHistoryTests()
    {
        _notifications = new NotificationService(new SqlNotificationRepository(_db));
        _history = new HistoryService(new SqlHistoryRepository(_db));
        _events = new QueueEventHandler(_notifications, _history);
        _userId = _db.AddUser(Email);
    }

    public void Dispose() => _db.Dispose();

    private string User => _userId.ToString();

    // ---------- notifications: saved and read back ----------

    [Fact]
    public void NotifyJoined_SavesRowInNotificationTable()
    {
        var note = _notifications.NotifyJoined(User, Service, 5, 25);

        Assert.NotNull(note);
        Assert.True(note!.Id > 0);
        Assert.Equal(1, _db.Scalar<int>("SELECT COUNT(*) FROM Notification WHERE UserId = @_userId", new { _userId }));
        Assert.Equal("Sent", _db.Scalar<string>("SELECT Status FROM Notification WHERE NotificationId = @Id", new { note.Id }));
    }

    [Fact]
    public void Notifications_PersistAcrossServiceInstances()
    {
        _notifications.NotifyJoined(User, Service, 5, 25);

        // a brand-new service (like a new request after a restart) still sees the data
        var fresh = new NotificationService(new SqlNotificationRepository(_db));
        var list = fresh.GetForUser(User);

        Assert.Single(list);
        Assert.Equal("You joined the queue for Dr. Cole", list[0].Title);
        Assert.Equal(User, list[0].UserId);
    }

    [Fact]
    public void UserCanBeIdentifiedByEmailOrId()
    {
        _notifications.NotifyJoined(Email, Service, 5, 25);

        Assert.Single(_notifications.GetForUser(User));
        Assert.Single(_notifications.GetForUser("  PATIENT@test.com "));
    }

    [Fact]
    public void GetForUser_FiltersByType_NewestFirst()
    {
        _notifications.NotifyJoined(User, Service, 5, 25);
        _notifications.NotifyStatusChange(User, "Served", "You are being served.");

        var all = _notifications.GetForUser(User);
        var status = _notifications.GetForUser(User, "status");

        Assert.Equal(2, all.Count);
        Assert.Equal("Served", all[0].Title);
        Assert.Single(status);
        Assert.Equal(NotificationTypes.Status, status[0].Type);
    }

    [Fact]
    public void MarkRead_SetsStatusViewed_AndUpdatesUnreadCount()
    {
        var first = _notifications.NotifyJoined(User, Service, 5, 25)!;
        _notifications.NotifyStatusChange(User, "Served", "You are being served.");
        Assert.Equal(2, _notifications.UnreadCount(User));

        Assert.True(_notifications.MarkRead(User, first.Id));

        Assert.Equal(1, _notifications.UnreadCount(User));
        Assert.Equal("Viewed", _db.Scalar<string>("SELECT Status FROM Notification WHERE NotificationId = @Id", new { first.Id }));
        Assert.True(_notifications.GetForUser(User).Find(n => n.Id == first.Id)!.Read);
    }

    [Fact]
    public void MarkRead_ReturnsFalse_ForMissingOrOtherUsersNotification()
    {
        var other = _db.AddUser("other@test.com");
        var note = _notifications.NotifyJoined(other.ToString(), Service, 5, 25)!;

        Assert.False(_notifications.MarkRead(User, note.Id));
        Assert.False(_notifications.MarkRead(User, 9999));
        Assert.False(_notifications.MarkRead("nobody@test.com", note.Id));
    }

    [Fact]
    public void MarkAllRead_And_Clear_OnlyAffectThatUser()
    {
        var other = _db.AddUser("other@test.com").ToString();
        _notifications.NotifyJoined(User, Service, 5, 25);
        _notifications.NotifyStatusChange(User, "Served", "You are being served.");
        _notifications.NotifyJoined(other, Service, 5, 25);

        Assert.Equal(2, _notifications.MarkAllRead(User));
        Assert.Equal(0, _notifications.UnreadCount(User));
        Assert.Equal(1, _notifications.UnreadCount(other));

        Assert.Equal(2, _notifications.ClearForUser(User));
        Assert.Empty(_notifications.GetForUser(User));
        Assert.Single(_notifications.GetForUser(other));
    }

    [Fact]
    public void UnknownUser_ReadsAreEmpty_ButWritesAreRejected()
    {
        Assert.Empty(_notifications.GetForUser("ghost@test.com"));
        Assert.Equal(0, _notifications.UnreadCount("ghost@test.com"));
        Assert.Equal(0, _notifications.MarkAllRead("ghost@test.com"));
        Assert.Equal(0, _notifications.ClearForUser("ghost@test.com"));

        Assert.Throws<KeyNotFoundException>(() => _notifications.NotifyJoined("ghost@test.com", Service, 5, 25));
    }

    // ---------- settings: stored in UserProfile ----------

    [Fact]
    public void Settings_DefaultsComeFromUserProfile()
    {
        var settings = _notifications.GetSettings(User);

        Assert.True(settings.QueueUpdates);
        Assert.True(settings.StatusChanges);
        Assert.Equal(3, settings.AlertWithinSpots);
        Assert.True(settings.Popups);
    }

    [Fact]
    public void UpdateSettings_SavesToUserProfile_AndIsUsedForAlerts()
    {
        _notifications.UpdateSettings(User, new NotificationSettingsRequest(true, false, 5, false));

        Assert.Equal(5, _db.Scalar<int>("SELECT AlertWithinSpots FROM UserProfile WHERE UserId = @_userId", new { _userId }));
        var fresh = new NotificationService(new SqlNotificationRepository(_db));
        var saved = fresh.GetSettings(User);
        Assert.False(saved.StatusChanges);
        Assert.False(saved.Popups);

        // status changes are now off, so no status notification is stored
        Assert.Null(fresh.NotifyStatusChange(User, "Served", "You are being served."));
        // alert range is 5, so position 5 gets an "almost up" alert
        Assert.NotNull(fresh.CheckCloseToServed(User, Service, 5));
    }

    [Fact]
    public void UpdateSettings_UnknownUser_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            _notifications.UpdateSettings("ghost@test.com", new NotificationSettingsRequest(true, true, 3, true)));
    }

    [Fact]
    public void GetSettings_UnknownUser_ReturnsDefaults()
    {
        var settings = _notifications.GetSettings("ghost@test.com");
        Assert.Equal(3, settings.AlertWithinSpots);
    }

    // ---------- history: VisitHistory table ----------

    [Fact]
    public void RecordJoin_SavesWaitingVisit()
    {
        var entry = _history.RecordJoin(User, Service);

        Assert.True(entry.Id > 0);
        Assert.Equal(HistoryOutcomes.Waiting, entry.Outcome);
        Assert.Equal("Waiting", _db.Scalar<string>("SELECT Outcome FROM VisitHistory WHERE HistoryId = @Id", new { entry.Id }));
    }

    [Fact]
    public void RecordJoin_Twice_ForSameService_IsRejected_EvenIgnoringCase()
    {
        _history.RecordJoin(User, Service);
        Assert.Throws<InvalidOperationException>(() => _history.RecordJoin(User, "dr. cole"));
    }

    [Fact]
    public void CompleteLatest_UpdatesOutcomeAndEndTime()
    {
        var entry = _history.RecordJoin(User, Service);

        var done = _history.CompleteLatest(User, Service, "served");

        Assert.Equal(HistoryOutcomes.Served, done.Outcome);
        Assert.NotNull(done.EndedAt);
        Assert.Equal("Served", _db.Scalar<string>("SELECT Outcome FROM VisitHistory WHERE HistoryId = @Id", new { entry.Id }));
        Assert.NotNull(_db.Scalar<string?>("SELECT EndedAt FROM VisitHistory WHERE HistoryId = @Id", new { entry.Id }));
    }

    [Fact]
    public void CompleteLatest_WithoutOpenVisit_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _history.CompleteLatest(User, Service, "left"));
    }

    [Fact]
    public void History_PersistsAndSummarises_NewestFirst()
    {
        _history.RecordJoin(User, Service);
        _history.CompleteLatest(User, Service, "served");
        _history.RecordJoin(User, "Dr. Nate");
        _history.CompleteLatest(User, "Dr. Nate", "left");
        _history.RecordJoin(User, Service);

        var fresh = new HistoryService(new SqlHistoryRepository(_db));
        var list = fresh.GetForUser(Email);
        var summary = fresh.GetSummary(User);

        Assert.Equal(3, list.Count);
        Assert.Equal(HistoryOutcomes.Waiting, list[0].Outcome);
        Assert.Equal("Dr. Nate", list[1].ServiceName);
        Assert.Equal(new HistorySummary(3, 1, 1, 1), summary);
    }

    [Fact]
    public void History_UnknownUser()
    {
        Assert.Empty(_history.GetForUser("ghost@test.com"));
        Assert.Throws<KeyNotFoundException>(() => _history.RecordJoin("ghost@test.com", Service));
        Assert.Throws<KeyNotFoundException>(() => _history.CompleteLatest("ghost@test.com", Service, "left"));
    }

    // ---------- the queue events that tie both together ----------

    [Fact]
    public void QueueEvents_JoinThenServe_StoreHistoryAndNotifications()
    {
        var joined = _events.OnUserJoined(User, Service, 2, 30);
        var served = _events.OnUserServed(User, Service);

        Assert.Equal(2, joined.Notifications.Count);   // "joined" + "almost up" (position 2 <= 3)
        Assert.Equal(HistoryOutcomes.Served, served.HistoryEntry.Outcome);
        Assert.NotNull(served.Notification);
        Assert.Equal(3, _db.Scalar<int>("SELECT COUNT(*) FROM Notification"));
        Assert.Equal(1, _db.Scalar<int>("SELECT COUNT(*) FROM VisitHistory WHERE Outcome = 'Served'"));
    }

    // ---------- database setup helpers ----------

    [Fact]
    public void Pick_UsesInMemory_WhenNoDatabaseIsRegistered()
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        var repo = DatabaseSetup.Pick<INotificationRepository>(provider,
            db => new SqlNotificationRepository(db), () => new InMemoryNotificationRepository());
        Assert.IsType<InMemoryNotificationRepository>(repo);
    }

    [Fact]
    public void Pick_UsesSql_WhenDatabaseIsRegistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbConnectionFactory>(_db);
        var repo = DatabaseSetup.Pick<INotificationRepository>(services.BuildServiceProvider(),
            db => new SqlNotificationRepository(db), () => new InMemoryNotificationRepository());
        Assert.IsType<SqlNotificationRepository>(repo);
    }

    [Fact]
    public void AddDatabase_RegistersSqlServer_OnlyWhenConnectionStringIsSet()
    {
        var withDb = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:ClinicDb"] = "Server=x;Database=y" })
            .Build();
        withDb.AddDatabase(config);

        var withoutDb = new ServiceCollection();
        withoutDb.AddDatabase(new ConfigurationBuilder().Build());

        var factory = withDb.BuildServiceProvider().GetService<IDbConnectionFactory>();
        Assert.IsType<SqlServerConnectionFactory>(factory);
        Assert.Contains("SCOPE_IDENTITY", factory!.SelectLastId);
        Assert.Null(withoutDb.BuildServiceProvider().GetService<IDbConnectionFactory>());
    }
}
