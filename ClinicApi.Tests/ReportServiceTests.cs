using System;
using System.Collections.Generic;
using System.Linq;
using ClinicApi.Data;
using ClinicApi.Services;
using Xunit;

namespace ClinicApi.Tests;

/// <summary>Final project: Reporting module (users + history, services + activity, statistics, CSV).</summary>
public class ReportServiceTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly ReportService _reports;
    private readonly int _nate, _cole, _nateQueue, _coleQueue;
    private readonly int _ana, _ben, _admin;

    // Mon Nov 2, 2026 at 9:00 AM clinic time
    private static readonly DateTime Nov2At9 = ClinicTime.ToUtc(new DateTime(2026, 11, 2, 9, 0, 0));

    public ReportServiceTests()
    {
        _reports = new ReportService(new SqlReportRepository(_db));
        _nate = _db.AddService("Dr. Nate", durationMinutes: 60, priority: "High");
        _cole = _db.AddService("Dr. Cole", durationMinutes: 30, priority: "Low");
        _nateQueue = _db.AddQueue(_nate);
        _coleQueue = _db.AddQueue(_cole, "Closed");
        _ana = _db.AddUser("ana@test.com", firstName: "Ana", lastName: "Lopez");
        _ben = _db.AddUser("ben@test.com", firstName: "Ben", lastName: "Ng");
        _admin = _db.AddUser("admin@test.com", role: "Admin", firstName: "Clinic", lastName: "Admin");
    }

    public void Dispose() => _db.Dispose();

    private ReportFilter Filter(string? from = null, string? to = null, string? service = null) =>
        _reports.ParseFilter(from, to, service);

    /// <summary>Ana: served by Nate after 30 min, canceled at Cole. Ben: served by Nate after 50 min a day later.</summary>
    private void AddTypicalDay()
    {
        _db.AddEntry(_nateQueue, _ana, Nov2At9, "Served", Nov2At9.AddMinutes(30));
        _db.AddEntry(_coleQueue, _ana, Nov2At9.AddHours(2), "Canceled", Nov2At9.AddHours(2).AddMinutes(5));
        _db.AddEntry(_nateQueue, _ben, Nov2At9.AddDays(1), "Served", Nov2At9.AddDays(1).AddMinutes(50));
    }

    // ---------- users + queue history ----------

    [Fact]
    public void UsersReport_NoFilter_ListsEveryPatient_ButNotAdmins()
    {
        _db.AddEntry(_nateQueue, _ana, Nov2At9, "Served", Nov2At9.AddMinutes(30));

        var rows = _reports.UsersReport(Filter());

        Assert.Equal(2, rows.Count);
        Assert.Equal("Ana Lopez", rows[0].Name);            // most visits first
        Assert.Equal(0, rows.Single(r => r.Email == "ben@test.com").TotalVisits);
        Assert.DoesNotContain(rows, r => r.UserId == _admin);
    }

    [Fact]
    public void UsersReport_CountsOutcomes_AndAverageWait_AndListsVisitsNewestFirst()
    {
        AddTypicalDay();

        var ana = _reports.UsersReport(Filter()).Single(r => r.UserId == _ana);

        Assert.Equal(2, ana.TotalVisits);
        Assert.Equal(1, ana.Served);
        Assert.Equal(1, ana.Canceled);
        Assert.Equal(0, ana.Waiting);
        Assert.Equal(30.0, ana.AvgWaitMinutes);
        Assert.Equal("Dr. Cole", ana.Visits[0].Service);
        Assert.Null(ana.Visits[0].WaitMinutes);              // canceled visits have no wait
        Assert.Equal(30.0, ana.Visits[1].WaitMinutes);
        Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0), ana.Visits[1].JoinedAt);   // shown in clinic time
    }

    [Fact]
    public void UsersReport_WithFilter_OnlyListsPeopleWhoCame()
    {
        AddTypicalDay();

        var rows = _reports.UsersReport(Filter(from: "2026-11-03", to: "2026-11-03"));

        var ben = Assert.Single(rows);
        Assert.Equal(_ben, ben.UserId);
        Assert.Equal(50.0, ben.AvgWaitMinutes);
    }

    [Fact]
    public void UsersReport_ServiceFilter_OnlyCountsThatService()
    {
        AddTypicalDay();

        var rows = _reports.UsersReport(Filter(service: "dr. cole"));

        var ana = Assert.Single(rows);
        Assert.Equal(1, ana.Canceled);
        Assert.Null(ana.AvgWaitMinutes);
    }

    [Fact]
    public void UsersReport_UserWithoutProfileName_ShowsEmail()
    {
        _db.Scalar<int>("UPDATE UserProfile SET FirstName = '', LastName = '' WHERE UserId = @id", new { id = _ben });

        var ben = _reports.UsersReport(Filter()).Single(r => r.UserId == _ben);

        Assert.Equal("ben@test.com", ben.Name);
    }

    // ---------- services + queue activity ----------

    [Fact]
    public void ServicesReport_ShowsDetailsAndActivity_ForEachService()
    {
        AddTypicalDay();
        _db.AddEntry(_nateQueue, _ana, Nov2At9.AddDays(5), "Waiting");

        var rows = _reports.ServicesReport(Filter());

        Assert.Equal(new[] { "Dr. Cole", "Dr. Nate" }, rows.Select(r => r.Name).ToArray());
        var nate = rows.Single(r => r.Name == "Dr. Nate");
        Assert.Equal(60, nate.DurationMinutes);
        Assert.Equal("High", nate.Priority);
        Assert.Equal("Open", nate.QueueStatus);
        Assert.Equal(3, nate.TotalJoined);
        Assert.Equal(2, nate.Served);
        Assert.Equal(1, nate.CurrentlyWaiting);
        Assert.Equal(40.0, nate.AvgWaitMinutes);
        var cole = rows.Single(r => r.Name == "Dr. Cole");
        Assert.Equal("Closed", cole.QueueStatus);
        Assert.Equal(1, cole.Canceled);
        Assert.Null(cole.AvgWaitMinutes);
    }

    [Fact]
    public void ServicesReport_WaitingNow_IgnoresTheDateFilter()
    {
        _db.AddEntry(_nateQueue, _ana, Nov2At9, "Waiting");

        var nate = _reports.ServicesReport(Filter(from: "2026-12-01", to: "2026-12-31")).Single(r => r.Name == "Dr. Nate");

        Assert.Equal(0, nate.TotalJoined);
        Assert.Equal(1, nate.CurrentlyWaiting);
    }

    [Fact]
    public void ServicesReport_ServiceWithoutQueue_ShowsOpen_AndServiceFilterWorks()
    {
        _db.AddService("Dr. Valdez", durationMinutes: 45);

        var rows = _reports.ServicesReport(Filter(service: "Dr. Valdez"));

        var valdez = Assert.Single(rows);
        Assert.Equal("Open", valdez.QueueStatus);
        Assert.Equal(0, valdez.TotalJoined);
    }

    // ---------- statistics ----------

    [Fact]
    public void Stats_CountsServedCanceledWaiting_AndAveragesWait()
    {
        AddTypicalDay();
        _db.AddEntry(_nateQueue, _ana, Nov2At9.AddDays(1).AddMinutes(10), "Waiting");

        var stats = _reports.Stats(Filter());

        Assert.Equal(4, stats.TotalJoined);
        Assert.Equal(2, stats.Served);
        Assert.Equal(1, stats.Canceled);
        Assert.Equal(1, stats.StillWaiting);
        Assert.Equal(40.0, stats.AvgWaitMinutes);
        Assert.Equal(50.0, stats.LongestWaitMinutes);
        Assert.Equal("Dr. Nate", stats.BusiestService);
        Assert.Equal(9, stats.BusiestHour);                  // clinic time, not UTC
        Assert.Equal(66.7, stats.ServedRatePercent);
        Assert.Null(stats.From);
    }

    [Fact]
    public void Stats_NoVisits_ReturnsZerosAndBlanks()
    {
        var stats = _reports.Stats(Filter());

        Assert.Equal(0, stats.TotalJoined);
        Assert.Null(stats.AvgWaitMinutes);
        Assert.Null(stats.BusiestService);
        Assert.Null(stats.BusiestHour);
        Assert.Equal(0, stats.ServedRatePercent);
    }

    [Fact]
    public void Stats_ToDateIncludesTheWholeDay_InClinicTime()
    {
        var lateNov2 = ClinicTime.ToUtc(new DateTime(2026, 11, 2, 23, 30, 0));   // already Nov 3 in UTC
        _db.AddEntry(_nateQueue, _ana, lateNov2, "Served", lateNov2.AddMinutes(10));
        _db.AddEntry(_nateQueue, _ben, lateNov2.AddHours(1), "Served", lateNov2.AddHours(1).AddMinutes(10));

        var stats = _reports.Stats(Filter(from: "2026-11-02", to: "2026-11-02"));

        Assert.Equal(1, stats.TotalJoined);
        Assert.Equal("2026-11-02", stats.To);
    }

    // ---------- filter validation ----------

    [Theory]
    [InlineData("11/02/2026", null, "from")]
    [InlineData(null, "yesterday", "to")]
    [InlineData("2026-11-05", "2026-11-01", "to")]
    [InlineData("2025-01-01", "2026-12-31", "to")]
    public void ParseFilter_BadDates_ReportTheField(string? from, string? to, string field)
    {
        var ex = Assert.Throws<ApiValidationException>(() => _reports.ParseFilter(from, to, null));

        Assert.True(ex.Errors.ContainsKey(field));
    }

    [Fact]
    public void ParseFilter_ServiceTooLong_IsInvalid()
    {
        var ex = Assert.Throws<ApiValidationException>(() => _reports.ParseFilter(null, null, new string('x', 101)));

        Assert.True(ex.Errors.ContainsKey("service"));
    }

    [Fact]
    public void ParseFilter_UnknownService_IsNotFound()
    {
        Assert.Throws<KeyNotFoundException>(() => _reports.ParseFilter(null, null, "Dr. Nobody"));
    }

    [Fact]
    public void ParseFilter_TrimsService_AndBlankValuesMeanNoFilter()
    {
        var filter = _reports.ParseFilter(" ", "", "  Dr. Nate ");

        Assert.Null(filter.From);
        Assert.Null(filter.To);
        Assert.Equal("Dr. Nate", filter.Service);
    }

    [Fact]
    public void NoDatabase_ReportsAreUnavailable()
    {
        var reports = new ReportService(null);

        Assert.False(reports.Available);
        Assert.Throws<InvalidOperationException>(() => reports.Stats(new ReportFilter(null, null, null)));
    }

    // ---------- wait time rule ----------

    [Fact]
    public void WaitMinutes_OnlyForServedVisitsWithAnEndTime()
    {
        var joined = new DateTime(2026, 11, 2, 15, 0, 0);

        Assert.Equal(12.5, ReportService.WaitMinutes(new VisitRecord { Status = "Served", JoinTime = joined, EndedAt = joined.AddMinutes(12.5) }));
        Assert.Null(ReportService.WaitMinutes(new VisitRecord { Status = "Canceled", JoinTime = joined, EndedAt = joined.AddMinutes(5) }));
        Assert.Null(ReportService.WaitMinutes(new VisitRecord { Status = "Served", JoinTime = joined, EndedAt = null }));
        Assert.Null(ReportService.WaitMinutes(new VisitRecord { Status = "Served", JoinTime = joined, EndedAt = joined.AddMinutes(-1) }));
    }

    // ---------- CSV ----------

    [Fact]
    public void UsersCsv_HasHeader_OneRowPerVisit_AndPatientsWithoutVisits()
    {
        AddTypicalDay();
        _db.AddUser("cara@test.com", firstName: "Cara", lastName: "Diaz");

        var lines = _reports.UsersCsv(Filter()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("User ID,Name,Email,Service,Joined,Ended,Outcome,Wait (min),Priority", lines[0]);
        Assert.Equal(1 + 3 + 1, lines.Length);   // header + 3 visits + Cara with no visits
        Assert.Contains($"{_ana},Ana Lopez,ana@test.com,Dr. Nate,2026-11-02 09:00,2026-11-02 09:30,Served,30,Standard", lines);
        Assert.Contains(lines, l => l.Contains("Cara Diaz") && l.Contains("No visits"));
    }

    [Fact]
    public void ServicesCsv_HasOneRowPerService()
    {
        AddTypicalDay();

        var lines = _reports.ServicesCsv(Filter()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("Service ID,Service,Description", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.Contains($"{_nate},Dr. Nate,Test service,60,High,Open,0,2,2,0,40", lines);
    }

    [Fact]
    public void StatsCsv_ListsEveryMetric()
    {
        AddTypicalDay();

        var csv = _reports.StatsCsv(Filter(service: "Dr. Nate"));

        Assert.Contains("Metric,Value", csv);
        Assert.Contains("Service,Dr. Nate", csv);
        Assert.Contains("From,All time", csv);
        Assert.Contains("Served,2", csv);
        Assert.Contains("Average wait (min),40", csv);
        Assert.Contains("Busiest hour,09:00", csv);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("Lopez, Ana", "\"Lopez, Ana\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Escape_QuotesAndBlocksFormulas(string? value, string expected)
    {
        Assert.Equal(expected, ReportService.Escape(value));
    }

    [Theory]
    [InlineData(1, "Urgent")]
    [InlineData(2, "High")]
    [InlineData(3, "Standard")]
    public void PriorityName_MatchesTheQueueLevels(int priority, string expected)
    {
        Assert.Equal(expected, ReportService.PriorityName(priority));
    }
}
