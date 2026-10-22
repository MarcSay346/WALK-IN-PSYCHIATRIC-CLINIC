using System;
using System.Collections.Generic;
using ClinicApi.Services;
using Xunit;

namespace ClinicApi.Tests;

public class QueueEventHandlerTests
{
    private readonly NotificationService _notifications = new();
    private readonly HistoryService _history = new();
    private readonly QueueEventHandler _events;
    private const string User = "patient@test.com";
    private const string Service = "Urgent Consult";

    public QueueEventHandlerTests()
    {
        _events = new QueueEventHandler(_notifications, _history);
    }

    [Fact]
    public void OnUserJoined_FarFromFront_RecordsHistory_AndSendsJoinNotification()
    {
        var result = _events.OnUserJoined(User, Service, 6, 90);

        Assert.Equal(HistoryOutcomes.Waiting, result.HistoryEntry.Outcome);
        var note = Assert.Single(result.Notifications);
        Assert.Equal("You are #6 in line. Estimated wait: 1 hour 30 min.", note.Message);
        Assert.Single(_history.GetForUser(User));
    }

    [Fact]
    public void OnUserJoined_NearFront_AlsoSendsAlmostUp()
    {
        var result = _events.OnUserJoined(User, Service, 2, 10);

        Assert.Equal(2, result.Notifications.Count);
        Assert.Equal("You joined the queue for Urgent Consult", result.Notifications[0].Title);
        Assert.Equal("You're almost up: #2 in line", result.Notifications[1].Title);
    }

    [Fact]
    public void OnUserJoined_InvalidInput_SavesNothing()
    {
        Assert.Throws<ApiValidationException>(() => _events.OnUserJoined(User, Service, 0, 10));

        Assert.Empty(_history.GetForUser(User));
        Assert.Empty(_notifications.GetForUser(User));
    }

    [Fact]
    public void OnUserJoined_AlreadyInThatQueue_Throws_AndSendsNoDuplicate()
    {
        _events.OnUserJoined(User, Service, 6, 30);

        Assert.Throws<InvalidOperationException>(() => _events.OnUserJoined(User, Service, 6, 30));

        Assert.Single(_notifications.GetForUser(User));
    }

    [Fact]
    public void OnPositionChanged_NotifiesOnlyOnceCloseToFront()
    {
        _events.OnUserJoined(User, Service, 6, 30);

        Assert.Null(_events.OnPositionChanged(User, Service, 4));
        Assert.NotNull(_events.OnPositionChanged(User, Service, 3));
    }

    [Fact]
    public void OnUserServed_ClosesHistory_AndSendsStatusNotification()
    {
        _events.OnUserJoined(User, Service, 1, 0);

        var result = _events.OnUserServed(User, Service);

        Assert.Equal(HistoryOutcomes.Served, result.HistoryEntry.Outcome);
        Assert.Equal(NotificationTypes.Status, result.Notification!.Type);
        Assert.Equal("It's your turn", result.Notification.Title);
    }

    [Fact]
    public void OnUserLeft_ClosesHistory_AndSendsStatusNotification()
    {
        _events.OnUserJoined(User, Service, 4, 20);

        var result = _events.OnUserLeft(User, Service);

        Assert.Equal(HistoryOutcomes.Left, result.HistoryEntry.Outcome);
        Assert.Equal("You left the queue", result.Notification!.Title);
    }

    [Fact]
    public void OnUserServed_NotInQueue_ThrowsNotFound()
    {
        Assert.Throws<KeyNotFoundException>(() => _events.OnUserServed(User, Service));
    }

    [Fact]
    public void OnUserServed_StatusChangesOff_StillRecordsHistory()
    {
        _notifications.UpdateSettings(User, new NotificationSettingsRequest(true, false, 3, true));
        _events.OnUserJoined(User, Service, 1, 0);

        var result = _events.OnUserServed(User, Service);

        Assert.Null(result.Notification);
        Assert.Equal(HistoryOutcomes.Served, result.HistoryEntry.Outcome);
    }
}
