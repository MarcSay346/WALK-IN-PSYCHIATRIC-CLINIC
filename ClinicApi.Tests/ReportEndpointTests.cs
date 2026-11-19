using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ClinicApi.Data;
using ClinicApi.Endpoints;
using ClinicApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Xunit;

namespace ClinicApi.Tests;

/// <summary>Final project: report endpoints are admin-only and return JSON or a CSV download.</summary>
public class ReportEndpointTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly ReportService _reports;
    private readonly AccountService _accounts = new();   // in memory: patient@test.com = 1, admin@test.com = 2
    private const string Patient = "1";
    private const string Admin = "2";

    public ReportEndpointTests()
    {
        _reports = new ReportService(new SqlReportRepository(_db));
        var nate = _db.AddService("Dr. Nate", durationMinutes: 60, priority: "High");
        var queue = _db.AddQueue(nate);
        var ana = _db.AddUser("ana@test.com", firstName: "Ana", lastName: "Lopez");
        var joined = ClinicTime.ToUtc(new DateTime(2026, 11, 2, 9, 0, 0));
        _db.AddEntry(queue, ana, joined, "Served", joined.AddMinutes(20));
    }

    public void Dispose() => _db.Dispose();

    private static int StatusOf(IResult result) => ((IStatusCodeHttpResult)result).StatusCode ?? 0;

    private IResult Users(string? admin, string? from = null, string? to = null, string? service = null, string? format = null) =>
        ReportEndpoints.GetUsers(admin, from, to, service, format, _reports, _accounts);

    // ---------- admin only ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("999")]
    public void MissingOrUnknownUser_Returns401(string? header)
    {
        Assert.Equal(401, StatusOf(Users(header)));
    }

    [Fact]
    public void Patient_Returns403()
    {
        Assert.Equal(403, StatusOf(Users(Patient)));
    }

    [Fact]
    public void Admin_GetsTheUsersReport()
    {
        var result = Users(Admin);

        Assert.Equal(200, StatusOf(result));
        var rows = Assert.IsType<Ok<object>>(result).Value as List<UserReportRow>;
        Assert.NotNull(rows);
        Assert.Equal("Ana Lopez", Assert.Single(rows!).Name);
    }

    [Fact]
    public void CheckAdmin_AllowsAdmins()
    {
        Assert.Null(ReportEndpoints.CheckAdmin(" 2 ", _accounts));
    }

    // ---------- JSON reports ----------

    [Fact]
    public void Services_And_Stats_ReturnOk()
    {
        var services = ReportEndpoints.GetServices(Admin, null, null, null, "json", _reports, _accounts);
        var stats = ReportEndpoints.GetStats(Admin, "2026-11-01", "2026-11-30", "Dr. Nate", null, _reports, _accounts);

        Assert.Equal(200, StatusOf(services));
        Assert.Equal(200, StatusOf(stats));
        var body = Assert.IsType<StatsReport>(Assert.IsType<Ok<object>>(stats).Value);
        Assert.Equal(1, body.Served);
        Assert.Equal(20.0, body.AvgWaitMinutes);
    }

    // ---------- CSV ----------

    [Fact]
    public void FormatCsv_ReturnsADownloadableFile()
    {
        var file = Assert.IsType<FileContentHttpResult>(Users(Admin, format: "CSV"));

        Assert.Equal("text/csv", file.ContentType);
        Assert.StartsWith("users-report-", file.FileDownloadName);
        Assert.EndsWith(".csv", file.FileDownloadName);
        var bytes = file.FileContents.ToArray();
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3).ToArray());   // BOM so Excel reads UTF-8
        var text = Encoding.UTF8.GetString(bytes.Skip(3).ToArray());
        Assert.StartsWith("User ID,Name,Email", text);
        Assert.Contains("Ana Lopez", text);
    }

    [Fact]
    public void ServicesAndStatsCsv_UseTheirOwnFileNames()
    {
        var services = Assert.IsType<FileContentHttpResult>(
            ReportEndpoints.GetServices(Admin, null, null, null, "csv", _reports, _accounts));
        var stats = Assert.IsType<FileContentHttpResult>(
            ReportEndpoints.GetStats(Admin, null, null, null, "csv", _reports, _accounts));

        Assert.StartsWith("services-report-", services.FileDownloadName);
        Assert.StartsWith("stats-report-", stats.FileDownloadName);
    }

    // ---------- errors ----------

    [Fact]
    public void UnknownFormat_Returns400()
    {
        Assert.Equal(400, StatusOf(Users(Admin, format: "pdf")));
    }

    [Fact]
    public void BadDate_Returns400()
    {
        Assert.Equal(400, StatusOf(Users(Admin, from: "Nov 1")));
    }

    [Fact]
    public void UnknownService_Returns404()
    {
        Assert.Equal(404, StatusOf(Users(Admin, service: "Dr. Nobody")));
    }

    [Fact]
    public void NoDatabase_Returns503()
    {
        var result = ReportEndpoints.GetStats(Admin, null, null, null, null, new ReportService(null), _accounts);

        Assert.Equal(503, StatusOf(result));
    }

    [Fact]
    public void NonAdmin_IsCheckedBeforeTheDatabase()
    {
        var result = ReportEndpoints.GetStats(Patient, null, null, null, null, new ReportService(null), _accounts);

        Assert.Equal(403, StatusOf(result));
    }
}
