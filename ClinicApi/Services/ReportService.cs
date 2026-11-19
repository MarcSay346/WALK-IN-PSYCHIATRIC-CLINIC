using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ClinicApi.Data;

namespace ClinicApi.Services;

// ---------- what the report endpoints send back ----------

/// <summary>Filters after validation. From/To are clinic-local dates (both inclusive).</summary>
public record ReportFilter(DateOnly? From, DateOnly? To, string? Service)
{
    public DateTime? FromUtc => From is { } d ? ClinicTime.ToUtc(d.ToDateTime(TimeOnly.MinValue)) : null;
    public DateTime? ToUtcExclusive => To is { } d ? ClinicTime.ToUtc(d.AddDays(1).ToDateTime(TimeOnly.MinValue)) : null;
}

public record VisitLine(int EntryId, string Service, DateTime JoinedAt, DateTime? EndedAt, string Outcome, int Priority, double? WaitMinutes);

public record UserReportRow(int UserId, string Name, string Email, int TotalVisits, int Served, int Canceled, int Waiting,
    double? AvgWaitMinutes, List<VisitLine> Visits);

public record ServiceReportRow(int ServiceId, string Name, string Description, int DurationMinutes, string Priority,
    string QueueStatus, int CurrentlyWaiting, int TotalJoined, int Served, int Canceled, double? AvgWaitMinutes);

public record StatsReport(string? From, string? To, string? Service, int TotalJoined, int Served, int Canceled, int StillWaiting,
    double? AvgWaitMinutes, double? LongestWaitMinutes, string? BusiestService, int? BusiestHour, double ServedRatePercent);

/// <summary>
/// Reporting module (Final Project). Admin-only reports built from the queue tables:
///   - users and their queue participation history
///   - service details and queue activity
///   - queue usage statistics (served count, average wait, ...)
/// Every report can be returned as JSON (for the page) or CSV (download).
///
/// Wait time = time from joining the queue until the admin served the patient (QueueEntry.EndedAt).
/// The math is done here in C# so it works the same on SQL Server and the SQLite test database.
/// </summary>
public class ReportService
{
    public const int MaxRangeDays = 366;

    private readonly IReportRepository? _repo;

    /// <summary>repo is null when no database is configured (reports need the database).</summary>
    public ReportService(IReportRepository? repo) => _repo = repo;

    public bool Available => _repo is not null;

    private IReportRepository Repo =>
        _repo ?? throw new InvalidOperationException("Reports need the database. Set ConnectionStrings:ClinicDb and restart.");

    // ---------- filters ----------

    /// <summary>Validates the query string values. Dates use yyyy-MM-dd.</summary>
    public ReportFilter ParseFilter(string? from, string? to, string? service)
    {
        var errors = new Dictionary<string, string>();
        var fromDate = ParseDate(errors, "from", "From date", from);
        var toDate = ParseDate(errors, "to", "To date", to);

        if (fromDate is { } f && toDate is { } t)
        {
            if (f > t)
                errors["to"] = "To date must be on or after the From date.";
            else if (t.DayNumber - f.DayNumber + 1 > MaxRangeDays)
                errors["to"] = $"The date range can be at most {MaxRangeDays} days.";
        }
        if ((service?.Trim().Length ?? 0) > 100)
            errors["service"] = "Service must be 100 characters or fewer.";
        InputRules.ThrowIfInvalid(errors);

        var serviceName = string.IsNullOrWhiteSpace(service) ? null : service.Trim();
        if (serviceName is not null && !Repo.GetServices().Any(s => s.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase)))
            throw new KeyNotFoundException($"There is no service named '{serviceName}'.");

        return new ReportFilter(fromDate, toDate, serviceName);
    }

    private static DateOnly? ParseDate(IDictionary<string, string> errors, string field, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        errors[field] = $"{label} must be a date like 2026-11-30.";
        return null;
    }

    private bool HasFilter(ReportFilter f) => f.From is not null || f.To is not null || f.Service is not null;

    private List<VisitRecord> Visits(ReportFilter f) => Repo.GetVisits(f.FromUtc, f.ToUtcExclusive, f.Service);

    // ---------- 1. users and their queue history ----------

    public List<UserReportRow> UsersReport(ReportFilter filter)
    {
        var visitsByUser = Visits(filter).GroupBy(v => v.UserId).ToDictionary(g => g.Key, g => g.ToList());
        var rows = new List<UserReportRow>();

        foreach (var user in Repo.GetPatients())
        {
            visitsByUser.TryGetValue(user.UserId, out var visits);
            visits ??= new List<VisitRecord>();
            if (visits.Count == 0 && HasFilter(filter)) continue;   // filtered reports only list people who came

            var name = $"{user.FirstName} {user.LastName}".Trim();
            rows.Add(new UserReportRow(
                user.UserId,
                name.Length > 0 ? name : user.Email,
                user.Email,
                visits.Count,
                visits.Count(v => v.Status == "Served"),
                visits.Count(v => v.Status == "Canceled"),
                visits.Count(v => v.Status == "Waiting"),
                AverageWait(visits),
                visits.OrderByDescending(v => v.JoinTime).Select(ToLine).ToList()));
        }
        return rows.OrderByDescending(r => r.TotalVisits).ThenBy(r => r.Name).ToList();
    }

    // ---------- 2. services and queue activity ----------

    public List<ServiceReportRow> ServicesReport(ReportFilter filter)
    {
        var visits = Visits(filter);
        // "waiting now" is about right now, so it ignores the date filter
        var waitingNow = Repo.GetVisits(null, null, filter.Service)
            .Where(v => v.Status == "Waiting")
            .GroupBy(v => v.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return Repo.GetServices()
            .Where(s => filter.Service is null || s.Name.Equals(filter.Service, StringComparison.OrdinalIgnoreCase))
            .Select(s =>
            {
                var mine = visits.Where(v => v.ServiceName.Equals(s.Name, StringComparison.OrdinalIgnoreCase)).ToList();
                return new ServiceReportRow(
                    s.ServiceId, s.Name, s.Description, s.DurationMinutes, s.Priority,
                    s.QueueStatus ?? "Open",
                    waitingNow.GetValueOrDefault(s.Name),
                    mine.Count,
                    mine.Count(v => v.Status == "Served"),
                    mine.Count(v => v.Status == "Canceled"),
                    AverageWait(mine));
            })
            .ToList();
    }

    // ---------- 3. queue usage statistics ----------

    public StatsReport Stats(ReportFilter filter)
    {
        var visits = Visits(filter);
        var waits = visits.Select(WaitMinutes).Where(w => w is not null).Select(w => w!.Value).ToList();
        var served = visits.Count(v => v.Status == "Served");

        var busiestService = visits
            .GroupBy(v => v.ServiceName)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => g.Key).FirstOrDefault();

        int? busiestHour = visits.Count == 0 ? null : visits
            .GroupBy(v => ClinicTime.ToLocal(v.JoinTime).Hour)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .First().Key;

        var finished = served + visits.Count(v => v.Status == "Canceled");

        return new StatsReport(
            filter.From?.ToString("yyyy-MM-dd"), filter.To?.ToString("yyyy-MM-dd"), filter.Service,
            visits.Count,
            served,
            visits.Count(v => v.Status == "Canceled"),
            visits.Count(v => v.Status == "Waiting"),
            waits.Count == 0 ? null : Math.Round(waits.Average(), 1),
            waits.Count == 0 ? null : Math.Round(waits.Max(), 1),
            busiestService,
            busiestHour,
            finished == 0 ? 0 : Math.Round(100.0 * served / finished, 1));
    }

    // ---------- CSV export ----------

    public string UsersCsv(ReportFilter filter)
    {
        var csv = new StringBuilder();
        Line(csv, "User ID", "Name", "Email", "Service", "Joined", "Ended", "Outcome", "Wait (min)", "Priority");
        foreach (var user in UsersReport(filter))
        {
            if (user.Visits.Count == 0)
                Line(csv, user.UserId.ToString(), user.Name, user.Email, "", "", "", "No visits", "", "");
            foreach (var v in user.Visits)
                Line(csv, user.UserId.ToString(), user.Name, user.Email, v.Service, Format(v.JoinedAt), Format(v.EndedAt),
                    v.Outcome, Number(v.WaitMinutes), PriorityName(v.Priority));
        }
        return csv.ToString();
    }

    public string ServicesCsv(ReportFilter filter)
    {
        var csv = new StringBuilder();
        Line(csv, "Service ID", "Service", "Description", "Duration (min)", "Priority", "Queue Status",
            "Waiting Now", "Total Joined", "Served", "Canceled", "Avg Wait (min)");
        foreach (var s in ServicesReport(filter))
            Line(csv, s.ServiceId.ToString(), s.Name, s.Description, s.DurationMinutes.ToString(), s.Priority, s.QueueStatus,
                s.CurrentlyWaiting.ToString(), s.TotalJoined.ToString(), s.Served.ToString(), s.Canceled.ToString(),
                Number(s.AvgWaitMinutes));
        return csv.ToString();
    }

    public string StatsCsv(ReportFilter filter)
    {
        var s = Stats(filter);
        var csv = new StringBuilder();
        Line(csv, "Metric", "Value");
        Line(csv, "From", s.From ?? "All time");
        Line(csv, "To", s.To ?? "All time");
        Line(csv, "Service", s.Service ?? "All services");
        Line(csv, "Total joined", s.TotalJoined.ToString());
        Line(csv, "Served", s.Served.ToString());
        Line(csv, "Canceled", s.Canceled.ToString());
        Line(csv, "Still waiting", s.StillWaiting.ToString());
        Line(csv, "Served rate (%)", s.ServedRatePercent.ToString(CultureInfo.InvariantCulture));
        Line(csv, "Average wait (min)", Number(s.AvgWaitMinutes));
        Line(csv, "Longest wait (min)", Number(s.LongestWaitMinutes));
        Line(csv, "Busiest service", s.BusiestService ?? "");
        Line(csv, "Busiest hour", s.BusiestHour is { } h ? $"{h:00}:00" : "");
        return csv.ToString();
    }

    /// <summary>Quotes a CSV field when needed and stops spreadsheet formula injection (=, +, @).</summary>
    public static string Escape(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && "=+@\t".Contains(text[0])) text = "'" + text;
        return text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + text.Replace("\"", "\"\"") + "\""
            : text;
    }

    // ---------- helpers ----------

    private static void Line(StringBuilder csv, params string[] fields) =>
        csv.Append(string.Join(",", fields.Select(Escape))).Append("\r\n");

    /// <summary>Minutes from joining to being served; null for people who canceled or are still waiting.</summary>
    public static double? WaitMinutes(VisitRecord v) =>
        v.Status == "Served" && v.EndedAt is { } end && end >= v.JoinTime
            ? Math.Round((end - v.JoinTime).TotalMinutes, 1)
            : null;

    private static double? AverageWait(IEnumerable<VisitRecord> visits)
    {
        var waits = visits.Select(WaitMinutes).Where(w => w is not null).Select(w => w!.Value).ToList();
        return waits.Count == 0 ? null : Math.Round(waits.Average(), 1);
    }

    private static VisitLine ToLine(VisitRecord v) =>
        new(v.EntryId, v.ServiceName, ClinicTime.ToLocal(v.JoinTime),
            v.EndedAt is { } e ? ClinicTime.ToLocal(e) : null, v.Status, v.Priority, WaitMinutes(v));

    private static string Format(DateTime? local) =>
        local?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";

    private static string Number(double? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "";

    public static string PriorityName(int priority) => priority switch
    {
        1 => "Urgent",
        2 => "High",
        _ => "Standard"
    };
}
