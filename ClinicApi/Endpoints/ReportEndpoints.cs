using System;
using System.Text;
using ClinicApi.Data;
using ClinicApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClinicApi.Endpoints;

/// <summary>
/// Reporting module API (admin only).
///
///   GET /api/reports/users      -> users + their queue history
///   GET /api/reports/services   -> service details + queue activity
///   GET /api/reports/stats      -> served count, average wait, busiest service/hour
///
/// Query string (all optional): from=yyyy-MM-dd, to=yyyy-MM-dd, service=Dr. Nate, format=json|csv
/// Header (required): X-User-Id: the logged-in user's id. 401 if missing, 403 if that user isn't an Admin.
///
/// In Program.cs:
///   builder.Services.AddReports();
///   app.MapReportEndpoints();
/// </summary>
public static class ReportEndpoints
{
    public const string UserHeader = "X-User-Id";

    public static IServiceCollection AddReports(this IServiceCollection services)
    {
        services.AddSingleton(sp => DatabaseSetup.Pick<ReportService>(sp,
            db => new ReportService(new SqlReportRepository(db)),
            () => new ReportService(null)));
        return services;
    }

    public static WebApplication MapReportEndpoints(this WebApplication app)
    {
        var reports = app.MapGroup("/api/reports");
        reports.MapGet("/users", GetUsers);
        reports.MapGet("/services", GetServices);
        reports.MapGet("/stats", GetStats);
        return app;
    }

    public static IResult GetUsers([FromHeader(Name = UserHeader)] string? adminId,
        string? from, string? to, string? service, string? format,
        ReportService reports, AccountService accounts) =>
        Run(adminId, from, to, service, format, reports, accounts, "users",
            filter => reports.UsersReport(filter), filter => reports.UsersCsv(filter));

    public static IResult GetServices([FromHeader(Name = UserHeader)] string? adminId,
        string? from, string? to, string? service, string? format,
        ReportService reports, AccountService accounts) =>
        Run(adminId, from, to, service, format, reports, accounts, "services",
            filter => reports.ServicesReport(filter), filter => reports.ServicesCsv(filter));

    public static IResult GetStats([FromHeader(Name = UserHeader)] string? adminId,
        string? from, string? to, string? service, string? format,
        ReportService reports, AccountService accounts) =>
        Run(adminId, from, to, service, format, reports, accounts, "stats",
            filter => reports.Stats(filter), filter => reports.StatsCsv(filter));

    // ---------- shared steps for every report ----------

    private static IResult Run(string? adminId, string? from, string? to, string? service, string? format,
        ReportService reports, AccountService accounts, string name,
        Func<ReportFilter, object> json, Func<ReportFilter, string> csv)
    {
        var denied = CheckAdmin(adminId, accounts);
        if (denied is not null) return denied;

        if (!reports.Available)
            return Results.Json(new { error = "Reports need the database. Set ConnectionStrings:ClinicDb and restart." },
                statusCode: StatusCodes.Status503ServiceUnavailable);

        var kind = string.IsNullOrWhiteSpace(format) ? "json" : format.Trim().ToLowerInvariant();
        if (kind != "json" && kind != "csv")
            return Results.BadRequest(new { errors = new { format = "Format must be json or csv." } });

        return NotificationHistoryEndpoints.Handle(() =>
        {
            var filter = reports.ParseFilter(from, to, service);
            if (kind == "json") return Results.Ok(json(filter));

            // UTF-8 with BOM so Excel shows names with accents correctly
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv(filter))).ToArray();
            return Results.File(bytes, "text/csv", $"{name}-report-{DateTime.UtcNow:yyyy-MM-dd}.csv");
        });
    }

    /// <summary>null when the caller is an admin, otherwise the 401/403 result to send back.</summary>
    public static IResult? CheckAdmin(string? adminId, AccountService accounts)
    {
        if (string.IsNullOrWhiteSpace(adminId) || !int.TryParse(adminId.Trim(), out var userId))
            return Results.Json(new { error = "Please log in as an administrator." }, statusCode: StatusCodes.Status401Unauthorized);

        try
        {
            var user = accounts.GetProfile(userId);
            if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase)) return null;
        }
        catch (KeyNotFoundException)
        {
            return Results.Json(new { error = "Please log in as an administrator." }, statusCode: StatusCodes.Status401Unauthorized);
        }
        return Results.Json(new { error = "Only administrators can view reports." }, statusCode: StatusCodes.Status403Forbidden);
    }
}
