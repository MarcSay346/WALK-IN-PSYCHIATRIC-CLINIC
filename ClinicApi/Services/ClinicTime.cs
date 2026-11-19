using System;

namespace ClinicApi.Services;

/// <summary>
/// The database stores times in UTC. Reports and the smart wait feature group and show
/// times in the clinic's own time zone (Houston = US Central).
/// </summary>
public static class ClinicTime
{
    public static readonly TimeZoneInfo Zone = FindZone();

    private static TimeZoneInfo FindZone()
    {
        // IANA id works on Linux/macOS and modern Windows; the Windows id is the fallback
        foreach (var id in new[] { "America/Chicago", "Central Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    /// <summary>UTC (or unspecified, as read from the database) -> clinic local time.</summary>
    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>Clinic local time -> UTC.</summary>
    public static DateTime ToUtc(DateTime local) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
}
