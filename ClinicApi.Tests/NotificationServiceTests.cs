using ClinicApi.Services;
using Xunit;

namespace ClinicApi.Tests;

public class NotificationServiceTests
{
    private readonly NotificationService _service = new();
    private const string User = "patient@test.com";
    private const string Service = "Urgent Consult";

    // ---------- trigger 1: user joins a queue ----------

    [Fact]
    public void NotifyJoined_CreatesQueueNotification_WithPositionAndWait()
    {
        var note = _service.NotifyJoined(User, Service, 5, 25);

        Assert.NotNull(note);
        Assert.Equal(NotificationTypes.Queue, note!.Type);
        Assert.Equal("You joined the queue for Urgent Consult", note.Title);
        Assert.Equal("You are #5 in line. Estimated wait: 25 minutes.", note.Message);
        Assert.False(note.Read);
        Assert.Single(_service.GetForUser(User));
    }

    [Fact]
    public void NotifyJoined_TrimsUserAndServiceName()
    {
        var note = _service.NotifyJoined("  " + User + "  ", "  Urgent Consult  ", 4, 20);

        Assert.Equal(User, note!.UserId);
        Assert.Equal("You joined the queue for Urgent Consult", note.Title);
    }

    [Fact]
    public void NotifyJoined_QueueUpdatesTurnedOff_SendsNothing()
    {
        _service.UpdateSettings(User, new NotificationSettingsRequest(false, true, 3, true));

        Assert.Null(_service.NotifyJoined(User, Service, 5, 25));
        Assert.Empty(_service.GetForUser(User));
    }

    [Theory]
    [InlineData(null, "Urgent Consult", 1, 5, "userId")]
    [InlineData("   ", "Urgent Consult", 1, 5, "userId")]
    [InlineData("u1", null, 1, 5, "serviceName")]
    [InlineData("u1", "", 1, 5, "serviceName")]
    [InlineData("u1", "Urgent Consult", null, 5, "position")]
    [InlineData("u1", "Urgent Consult", 0, 5, "position")]
    [InlineData("u1", "Urgent Consult", 1001, 5, "position")]
    [InlineData("u1", "Urgent Consult", 1, null, "estimatedWaitMinutes")]
    [InlineData("u1", "Urgent Consult", 1, -1, "estimatedWaitMinutes")]
    [InlineData("u1", "Urgent Consult", 1, 1441, "estimatedWaitMinutes")]
    public void NotifyJoined_InvalidInput_ThrowsWithFieldError(string? userId, string? serviceName, int? position, int? wait, string field)
    {
        var ex = Assert.Throws<ApiValidationException>(() => _service.NotifyJoined(userId, serviceName, position, wait));

        Assert.True(ex.Errors.ContainsKey(field));
        Assert.Empty(_service.GetForUser("u1"));
    }

    [Fact]
    public void NotifyJoined_ServiceNameTooLong_Throws()
    {
        var longName = new string('x', NotificationService.MaxServiceNameLength + 1);

        var ex = Assert.Throws<ApiValidationException>(() => _service.NotifyJoined(User, longName, 1, 5));

        Assert.Equal("Service name must be 100 characters or fewer.", ex.Errors["serviceName"]);
    }

    [Fact]
    public void NotifyJoined_EverythingMissing_ReportsEveryError()
    {
        var ex = Assert.Throws<ApiValidationException>(() => _service.NotifyJoined(null, null, null, null));

        Assert.Equal(4, ex.Errors.Count);
        Assert.Equal("User ID is required.", ex.Errors["userId"]);
    }

    // ---------- trigger 2: user is close to being served ----------

    [Fact]
    public void CheckCloseToServed_OutsideAlertRange_SendsNothing()
    {
        Assert.Null(_service.CheckCloseToServed(User, Service, 4));   // default range is 3 spots
        Assert.Empty(_service.GetForUser(User));
    }

    [Theory]
    [InlineData(3, "You're almost up: #3 in line", "Only 2 people are ahead of you for Urgent Consult.")]
    [InlineData(2, "You're almost up: #2 in line", "Only 1 person is ahead of you for Urgent Consult.")]
    [InlineData(1, "You're next in line", "A provider for Urgent Consult will be ready for you shortly. Please stay nearby.")]
    public void CheckCloseToServed_InsideAlertRange_SendsAlmostUpMessage(int position, string title, string message)
    {
        var note = _service.CheckCloseToServed(User, Service, position);

        Assert.NotNull(note);
        Assert.Equal(NotificationTypes.Queue, note!.Type);
        Assert.Equal(title, note.Title);
        Assert.Equal(message, note.Message);
    }

    [Fact]
    public void CheckCloseToServed_SamePositionTwice_OnlyNotifiesOnce()
    {
        Assert.NotNull(_service.CheckCloseToServed(User, Service, 2));
        Assert.Null(_service.CheckCloseToServed(User, Service, 2));
        Assert.NotNull(_service.CheckCloseToServed(User, Service, 1));

        Assert.Equal(2, _service.GetForUser(User).Count);
    }

    [Fact]
    public void CheckCloseToServed_UsesTheUsersAlertRange()
    {
        _service.UpdateSettings(User, new NotificationSettingsRequest(true, true, 5, true));

        Assert.NotNull(_service.CheckCloseToServed(User, Service, 5));
        Assert.Null(_service.CheckCloseToServed("other-user", Service, 5));   // still on the default of 3
    }

    [Fact]
    public void CheckCloseToServed_QueueUpdatesTurnedOff_SendsNothing()
    {
        _service.UpdateSettings(User, new NotificationSettingsRequest(false, true, 3, true));

        Assert.Null(_service.CheckCloseToServed(User, Service, 1));
    }

    [Fact]
    public void NotifyJoined_StartsANewVisit_SoAlmostUpCanFireAgain()
    {
        _service.CheckCloseToServed(User, Service, 2);
        _service.NotifyJoined(User, Service, 2, 10);

        Assert.NotNull(_service.CheckCloseToServed(User, Service, 2));
    }

    [Fact]
    public void CheckCloseToServed_InvalidPosition_Throws()
    {
        var ex = Assert.Throws<ApiValidationException>(() => _service.CheckCloseToServed(User, Service, 0));

        Assert.True(ex.Errors.ContainsKey("position"));
    }

    // ---------- status changes ----------

    [Fact]
    public void NotifyStatusChange_CreatesStatusNotification()
    {
        var note = _service.NotifyStatusChange(User, "It's your turn", "Please head to the front desk.");

        Assert.Equal(NotificationTypes.Status, note!.Type);
        Assert.Equal("It's your turn", note.Title);
    }

    [Fact]
    public void NotifyStatusChange_StatusChangesTurnedOff_SendsNothing()
    {
        _service.UpdateSettings(User, new NotificationSettingsRequest(true, false, 3, true));

        Assert.Null(_service.NotifyStatusChange(User, "It's your turn", "Please head to the front desk."));
    }

    [Fact]
    public void NotifyStatusChange_MissingTitleAndMessage_Throws()
    {
        var ex = Assert.Throws<ApiValidationException>(() => _service.NotifyStatusChange(User, "", null));

        Assert.True(ex.Errors.ContainsKey("title"));
        Assert.True(ex.Errors.ContainsKey("message"));
    }

    [Fact]
    public void NotifyStatusChange_MessageTooLong_Throws()
    {
        var longMessage = new string('m', NotificationService.MaxMessageLength + 1);

        var ex = Assert.Throws<ApiValidationException>(() => _service.NotifyStatusChange(User, "Title", longMessage));

        Assert.True(ex.Errors.ContainsKey("message"));
    }

    // ---------- reading ----------

    [Fact]
    public void GetForUser_ReturnsNewestFirst_AndOnlyThatUsersNotifications()
    {
        _service.NotifyJoined(User, "Service A", 9, 10);
        _service.NotifyJoined("someone-else", "Service A", 9, 10);
        _service.NotifyStatusChange(User, "Newest", "Message");

        var list = _service.GetForUser(User);

        Assert.Equal(2, list.Count);
        Assert.Equal("Newest", list[0].Title);
        Assert.All(list, n => Assert.Equal(User, n.UserId));
    }

    [Theory]
    [InlineData("queue", 1)]
    [InlineData("status", 1)]
    [InlineData("all", 2)]
    [InlineData("QUEUE", 1)]
    [InlineData(null, 2)]
    public void GetForUser_FiltersByType(string? type, int expected)
    {
        _service.NotifyJoined(User, "Service A", 9, 10);
        _service.NotifyStatusChange(User, "Title", "Message");

        Assert.Equal(expected, _service.GetForUser(User, type).Count);
    }

    [Fact]
    public void GetForUser_UnknownType_Throws()
    {
        var ex = Assert.Throws<ApiValidationException>(() => _service.GetForUser(User, "email"));

        Assert.Equal("Type must be all, queue, or status.", ex.Errors["type"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetForUser_BlankUser_Throws(string? userId)
    {
        Assert.Throws<ApiValidationException>(() => _service.GetForUser(userId));
    }

    // ---------- read / unread ----------

    [Fact]
    public void MarkRead_MarksOnlyThatNotification()
    {
        var first = _service.NotifyJoined(User, "Service A", 9, 10)!;
        var second = _service.NotifyStatusChange(User, "Title", "Message")!;
        Assert.Equal(2, _service.UnreadCount(User));

        Assert.True(_service.MarkRead(User, first.Id));

        Assert.Equal(1, _service.UnreadCount(User));
        Assert.True(first.Read);
        Assert.False(second.Read);
    }

    [Fact]
    public void MarkRead_UnknownId_ReturnsFalse()
    {
        Assert.False(_service.MarkRead(User, 999));
    }

    [Fact]
    public void MarkRead_SomeoneElsesNotification_ReturnsFalse()
    {
        var note = _service.NotifyJoined("someone-else", "Service A", 9, 10)!;

        Assert.False(_service.MarkRead(User, note.Id));
        Assert.False(note.Read);
    }

    [Fact]
    public void MarkAllRead_ReturnsHowManyWereMarked()
    {
        _service.NotifyJoined(User, "Service A", 9, 10);
        _service.NotifyStatusChange(User, "Title", "Message");

        Assert.Equal(2, _service.MarkAllRead(User));
        Assert.Equal(0, _service.UnreadCount(User));
        Assert.Equal(0, _service.MarkAllRead(User));
    }

    [Fact]
    public void ClearForUser_RemovesOnlyThatUsersNotifications_AndResetsReminders()
    {
        _service.CheckCloseToServed(User, Service, 1);
        _service.NotifyJoined("someone-else", "Service A", 9, 10);

        Assert.Equal(1, _service.ClearForUser(User));

        Assert.Empty(_service.GetForUser(User));
        Assert.Single(_service.GetForUser("someone-else"));
        Assert.NotNull(_service.CheckCloseToServed(User, Service, 1));
    }

    // ---------- settings ----------

    [Fact]
    public void GetSettings_NewUser_GetsDefaults()
    {
        var settings = _service.GetSettings(User);

        Assert.True(settings.QueueUpdates);
        Assert.True(settings.StatusChanges);
        Assert.Equal(3, settings.AlertWithinSpots);
        Assert.True(settings.Popups);
    }

    [Fact]
    public void UpdateSettings_ValidRequest_IsSaved()
    {
        _service.UpdateSettings(User, new NotificationSettingsRequest(true, false, 7, false));

        var settings = _service.GetSettings(User);
        Assert.True(settings.QueueUpdates);
        Assert.False(settings.StatusChanges);
        Assert.Equal(7, settings.AlertWithinSpots);
        Assert.False(settings.Popups);
    }

    [Fact]
    public void UpdateSettings_BothTypesOff_Throws()
    {
        var ex = Assert.Throws<ApiValidationException>(() =>
            _service.UpdateSettings(User, new NotificationSettingsRequest(false, false, 3, true)));

        Assert.Equal("Choose at least one type of notification.", ex.Errors["types"]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(null)]
    public void UpdateSettings_AlertRangeInvalid_Throws(int? spots)
    {
        var ex = Assert.Throws<ApiValidationException>(() =>
            _service.UpdateSettings(User, new NotificationSettingsRequest(true, true, spots, true)));

        Assert.True(ex.Errors.ContainsKey("alertWithinSpots"));
    }

    [Fact]
    public void UpdateSettings_MissingChoices_Throws()
    {
        var ex = Assert.Throws<ApiValidationException>(() =>
            _service.UpdateSettings(User, new NotificationSettingsRequest(null, null, 3, true)));

        Assert.True(ex.Errors.ContainsKey("queueUpdates"));
        Assert.True(ex.Errors.ContainsKey("statusChanges"));
    }

    [Fact]
    public void UpdateSettings_NoBody_Throws()
    {
        var ex = Assert.Throws<ApiValidationException>(() => _service.UpdateSettings(User, null));

        Assert.Equal("Settings are required.", ex.Errors["body"]);
    }

    [Fact]
    public void UpdateSettings_BlankUser_Throws()
    {
        var ex = Assert.Throws<ApiValidationException>(() =>
            _service.UpdateSettings(" ", new NotificationSettingsRequest(true, true, 3, true)));

        Assert.True(ex.Errors.ContainsKey("userId"));
    }

    [Fact]
    public void UpdateSettings_PopupsLeftOut_KeepsPreviousChoice()
    {
        _service.UpdateSettings(User, new NotificationSettingsRequest(true, true, 3, false));
        _service.UpdateSettings(User, new NotificationSettingsRequest(true, true, 4, null));

        Assert.False(_service.GetSettings(User).Popups);
        Assert.Equal(4, _service.GetSettings(User).AlertWithinSpots);
    }

    [Fact]
    public void GetSettings_ReturnsACopy_SoCallersCannotChangeStoredSettings()
    {
        var copy = _service.GetSettings(User);
        copy.AlertWithinSpots = 9;

        Assert.Equal(3, _service.GetSettings(User).AlertWithinSpots);
    }

    // ---------- wait-time wording ----------

    [Theory]
    [InlineData(0, "less than a minute")]
    [InlineData(1, "1 minute")]
    [InlineData(25, "25 minutes")]
    [InlineData(60, "1 hour")]
    [InlineData(90, "1 hour 30 min")]
    [InlineData(125, "2 hours 5 min")]
    [InlineData(120, "2 hours")]
    public void FormatWait_ProducesReadableText(int minutes, string expected)
    {
        Assert.Equal(expected, NotificationService.FormatWait(minutes));
    }
}
