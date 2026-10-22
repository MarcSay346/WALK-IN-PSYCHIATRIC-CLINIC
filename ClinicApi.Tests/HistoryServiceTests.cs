using System;
using System.Collections.Generic;
using ClinicApi.Services;
using Xunit;

namespace ClinicApi.Tests;

public class HistoryServiceTests
{
    private readonly HistoryService _history = new();
    private const string User = "patient@test.com";

    [Fact]
    public void RecordJoin_CreatesWaitingEntry()
    {
        var before = DateTime.UtcNow;

        var entry = _history.RecordJoin("  " + User, " Urgent Consult ");

        Assert.Equal(User, entry.UserId);
        Assert.Equal("Urgent Consult", entry.ServiceName);
        Assert.Equal(HistoryOutcomes.Waiting, entry.Outcome);
        Assert.Null(entry.EndedAt);
        Assert.True(entry.JoinedAt >= before);
    }

    [Fact]
    public void RecordJoin_AlreadyWaitingForSameService_Throws()
    {
        _history.RecordJoin(User, "Urgent Consult");

        var ex = Assert.Throws<InvalidOperationException>(() => _history.RecordJoin(User, "urgent consult"));

        Assert.Contains("already waiting", ex.Message);
        Assert.Single(_history.GetForUser(User));
    }

    [Fact]
    public void RecordJoin_DifferentServiceOrDifferentUser_IsAllowed()
    {
        _history.RecordJoin(User, "Urgent Consult");
        _history.RecordJoin(User, "Medication Review");
        _history.RecordJoin("someone-else", "Urgent Consult");

        Assert.Equal(2, _history.GetForUser(User).Count);
        Assert.Single(_history.GetForUser("someone-else"));
    }

    [Fact]
    public void RecordJoin_AfterPreviousVisitEnded_IsAllowed()
    {
        _history.RecordJoin(User, "Urgent Consult");
        _history.CompleteLatest(User, "Urgent Consult", HistoryOutcomes.Served);

        _history.RecordJoin(User, "Urgent Consult");

        Assert.Equal(2, _history.GetForUser(User).Count);
    }

    [Theory]
    [InlineData(null, "Urgent Consult", "userId")]
    [InlineData("", "Urgent Consult", "userId")]
    [InlineData(User, null, "serviceName")]
    [InlineData(User, "   ", "serviceName")]
    public void RecordJoin_MissingFields_Throws(string? userId, string? serviceName, string field)
    {
        var ex = Assert.Throws<ApiValidationException>(() => _history.RecordJoin(userId, serviceName));

        Assert.True(ex.Errors.ContainsKey(field));
    }

    [Fact]
    public void RecordJoin_ServiceNameTooLong_Throws()
    {
        var longName = new string('x', HistoryService.MaxServiceNameLength + 1);

        var ex = Assert.Throws<ApiValidationException>(() => _history.RecordJoin(User, longName));

        Assert.True(ex.Errors.ContainsKey("serviceName"));
    }

    [Theory]
    [InlineData("Served", HistoryOutcomes.Served)]
    [InlineData("left", HistoryOutcomes.Left)]
    [InlineData("  SERVED ", HistoryOutcomes.Served)]
    public void CompleteLatest_SetsOutcomeAndEndTime(string outcome, string expected)
    {
        _history.RecordJoin(User, "Urgent Consult");

        var entry = _history.CompleteLatest(User, "Urgent Consult", outcome);

        Assert.Equal(expected, entry.Outcome);
        Assert.NotNull(entry.EndedAt);
        Assert.True(entry.EndedAt >= entry.JoinedAt);
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("")]
    [InlineData(null)]
    public void CompleteLatest_InvalidOutcome_Throws(string? outcome)
    {
        _history.RecordJoin(User, "Urgent Consult");

        var ex = Assert.Throws<ApiValidationException>(() => _history.CompleteLatest(User, "Urgent Consult", outcome));

        Assert.Equal("Outcome must be Served or Left.", ex.Errors["outcome"]);
    }

    [Fact]
    public void CompleteLatest_NoActiveVisit_ThrowsNotFound()
    {
        Assert.Throws<KeyNotFoundException>(() => _history.CompleteLatest(User, "Urgent Consult", "Served"));
    }

    [Fact]
    public void CompleteLatest_OnlyClosesTheOpenVisit()
    {
        _history.RecordJoin(User, "Urgent Consult");
        _history.CompleteLatest(User, "Urgent Consult", "Served");
        _history.RecordJoin(User, "Urgent Consult");
        _history.CompleteLatest(User, "Urgent Consult", "Left");

        var entries = _history.GetForUser(User);

        Assert.Equal(HistoryOutcomes.Left, entries[0].Outcome);
        Assert.Equal(HistoryOutcomes.Served, entries[1].Outcome);
    }

    [Fact]
    public void GetForUser_ReturnsNewestFirst_AndOnlyThatUser()
    {
        _history.RecordJoin(User, "First");
        _history.RecordJoin("someone-else", "Other");
        _history.RecordJoin(User, "Second");

        var entries = _history.GetForUser(User);

        Assert.Equal(new[] { "Second", "First" }, new[] { entries[0].ServiceName, entries[1].ServiceName });
    }

    [Fact]
    public void GetForUser_BlankUser_Throws()
    {
        Assert.Throws<ApiValidationException>(() => _history.GetForUser("  "));
    }

    [Fact]
    public void GetSummary_CountsEachOutcome()
    {
        _history.RecordJoin(User, "A");
        _history.CompleteLatest(User, "A", "Served");
        _history.RecordJoin(User, "B");
        _history.CompleteLatest(User, "B", "Left");
        _history.RecordJoin(User, "C");
        _history.RecordJoin(User, "A");

        var summary = _history.GetSummary(User);

        Assert.Equal(new HistorySummary(TotalVisits: 4, Served: 1, Left: 1, Waiting: 2), summary);
    }

    [Fact]
    public void GetSummary_NewUser_IsAllZero()
    {
        Assert.Equal(new HistorySummary(0, 0, 0, 0), _history.GetSummary(User));
    }
}
