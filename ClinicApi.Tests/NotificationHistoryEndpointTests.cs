using System.Collections.Generic;
using ClinicApi.Endpoints;
using ClinicApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicApi.Tests;

/// <summary>Checks that each endpoint returns the right HTTP status code (200 / 400 / 404 / 409).</summary>
public class NotificationHistoryEndpointTests
{
    private readonly NotificationService _notifications = new();
    private readonly HistoryService _history = new();
    private readonly QueueEventHandler _events;
    private const string User = "patient@test.com";
    private const string Service = "Urgent Consult";

    public NotificationHistoryEndpointTests()
    {
        _events = new QueueEventHandler(_notifications, _history);
    }

    private static int StatusOf(IResult result) => ((IStatusCodeHttpResult)result).StatusCode ?? 0;

    private static object? ValueOf(IResult result) => ((IValueHttpResult)result).Value;

    // ---------- notifications ----------

    [Fact]
    public void GetNotifications_ReturnsOkWithTheList()
    {
        _notifications.NotifyJoined(User, Service, 5, 25);

        var result = NotificationHistoryEndpoints.GetNotifications(User, null, _notifications);

        Assert.Equal(200, StatusOf(result));
        var list = Assert.IsType<List<Notification>>(ValueOf(result));
        Assert.Single(list);
    }

    [Fact]
    public void GetNotifications_UnknownType_ReturnsBadRequest()
    {
        var result = NotificationHistoryEndpoints.GetNotifications(User, "sms", _notifications);

        Assert.Equal(400, StatusOf(result));
    }

    [Fact]
    public void GetNotifications_BlankUser_ReturnsBadRequest()
    {
        Assert.Equal(400, StatusOf(NotificationHistoryEndpoints.GetNotifications(" ", null, _notifications)));
    }

    [Fact]
    public void GetUnreadCount_ReturnsOk()
    {
        Assert.Equal(200, StatusOf(NotificationHistoryEndpoints.GetUnreadCount(User, _notifications)));
    }

    [Fact]
    public void MarkRead_ExistingNotification_ReturnsOk()
    {
        var note = _notifications.NotifyJoined(User, Service, 5, 25)!;

        Assert.Equal(200, StatusOf(NotificationHistoryEndpoints.MarkRead(User, note.Id, _notifications)));
        Assert.True(note.Read);
    }

    [Fact]
    public void MarkRead_UnknownNotification_ReturnsNotFound()
    {
        Assert.Equal(404, StatusOf(NotificationHistoryEndpoints.MarkRead(User, 123, _notifications)));
    }

    [Fact]
    public void MarkAllRead_And_Clear_ReturnOk()
    {
        _notifications.NotifyJoined(User, Service, 5, 25);

        Assert.Equal(200, StatusOf(NotificationHistoryEndpoints.MarkAllRead(User, _notifications)));
        Assert.Equal(200, StatusOf(NotificationHistoryEndpoints.ClearNotifications(User, _notifications)));
        Assert.Empty(_notifications.GetForUser(User));
    }

    // ---------- settings ----------

    [Fact]
    public void GetSettings_ReturnsOk()
    {
        Assert.Equal(200, StatusOf(NotificationHistoryEndpoints.GetSettings(User, _notifications)));
    }

    [Fact]
    public void UpdateSettings_Valid_ReturnsOk()
    {
        var request = new NotificationSettingsRequest(true, true, 5, true);

        Assert.Equal(200, StatusOf(NotificationHistoryEndpoints.UpdateSettings(User, request, _notifications)));
    }

    [Fact]
    public void UpdateSettings_Invalid_ReturnsBadRequest()
    {
        var request = new NotificationSettingsRequest(false, false, 50, true);

        Assert.Equal(400, StatusOf(NotificationHistoryEndpoints.UpdateSettings(User, request, _notifications)));
    }

    [Fact]
    public void UpdateSettings_NoBody_ReturnsBadRequest()
    {
        Assert.Equal(400, StatusOf(NotificationHistoryEndpoints.UpdateSettings(User, null, _notifications)));
    }

    // ---------- queue events ----------

    [Fact]
    public void QueueJoined_Valid_ReturnsOkWithNotifications()
    {
        var result = NotificationHistoryEndpoints.QueueJoined(new QueueEventRequest(User, Service, 2, 10), _events);

        Assert.Equal(200, StatusOf(result));
        var joined = Assert.IsType<QueueJoinedResult>(ValueOf(result));
        Assert.Equal(2, joined.Notifications.Count);
    }

    [Fact]
    public void QueueJoined_NoBody_ReturnsBadRequest()
    {
        Assert.Equal(400, StatusOf(NotificationHistoryEndpoints.QueueJoined(null, _events)));
    }

    [Fact]
    public void QueueJoined_InvalidFields_ReturnsBadRequest()
    {
        var request = new QueueEventRequest("", Service, -3, 10);

        Assert.Equal(400, StatusOf(NotificationHistoryEndpoints.QueueJoined(request, _events)));
    }

    [Fact]
    public void QueueJoined_AlreadyInQueue_ReturnsConflict()
    {
        var request = new QueueEventRequest(User, Service, 4, 20);
        NotificationHistoryEndpoints.QueueJoined(request, _events);

        Assert.Equal(409, StatusOf(NotificationHistoryEndpoints.QueueJoined(request, _events)));
    }

    [Fact]
    public void QueuePositionChanged_ReturnsOk()
    {
        var request = new QueueEventRequest(User, Service, 1, null);

        Assert.Equal(200, StatusOf(NotificationHistoryEndpoints.QueuePositionChanged(request, _events)));
    }

    [Fact]
    public void QueueServed_NotInQueue_ReturnsNotFound()
    {
        var request = new QueueEventRequest(User, Service, null, null);

        Assert.Equal(404, StatusOf(NotificationHistoryEndpoints.QueueServed(request, _events)));
    }

    [Fact]
    public void QueueServed_And_QueueLeft_AfterJoining_ReturnOk()
    {
        NotificationHistoryEndpoints.QueueJoined(new QueueEventRequest(User, "Service A", 3, 15), _events);
        NotificationHistoryEndpoints.QueueJoined(new QueueEventRequest(User, "Service B", 3, 15), _events);

        Assert.Equal(200, StatusOf(NotificationHistoryEndpoints.QueueServed(new QueueEventRequest(User, "Service A", null, null), _events)));
        Assert.Equal(200, StatusOf(NotificationHistoryEndpoints.QueueLeft(new QueueEventRequest(User, "Service B", null, null), _events)));
    }

    // ---------- history ----------

    [Fact]
    public void GetHistory_And_Summary_ReturnOk()
    {
        _events.OnUserJoined(User, Service, 4, 20);

        var history = NotificationHistoryEndpoints.GetHistory(User, _history);
        Assert.Equal(200, StatusOf(history));
        Assert.Single(Assert.IsType<List<HistoryEntry>>(ValueOf(history)));

        Assert.Equal(200, StatusOf(NotificationHistoryEndpoints.GetHistorySummary(User, _history)));
    }

    [Fact]
    public void GetHistory_BlankUser_ReturnsBadRequest()
    {
        Assert.Equal(400, StatusOf(NotificationHistoryEndpoints.GetHistory("", _history)));
    }

    // ---------- setup ----------

    [Fact]
    public void AddNotificationAndHistory_RegistersSharedServices()
    {
        var services = new ServiceCollection();
        services.AddNotificationAndHistory();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<NotificationService>();
        Assert.Same(first, provider.GetRequiredService<NotificationService>());
        Assert.NotNull(provider.GetRequiredService<HistoryService>());
        Assert.NotNull(provider.GetRequiredService<QueueEventHandler>());
    }
}
