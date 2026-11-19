using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace ClinicApi.Data;

/// <summary>One queue visit (a QueueEntry row) with the user and service it belongs to.</summary>
public class VisitRecord
{
    public int EntryId { get; set; }
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public DateTime JoinTime { get; set; }
    public DateTime? EndedAt { get; set; }
    public string Status { get; set; } = "Waiting";   // Waiting / Served / Canceled
    public int Priority { get; set; } = 3;
}

/// <summary>One service with the status of its latest queue.</summary>
public class ServiceRecord
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Priority { get; set; } = "";
    public string? QueueStatus { get; set; }          // null = no queue yet (opens on the first join)
}

/// <summary>One patient account (admins are left out of reports).</summary>
public class UserRecord
{
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

/// <summary>Read-only queries used by the reporting module.</summary>
public interface IReportRepository
{
    /// <summary>Visits that started in [fromUtc, toUtcExclusive), optionally for one service. Oldest first.</summary>
    List<VisitRecord> GetVisits(DateTime? fromUtc, DateTime? toUtcExclusive, string? serviceName);

    List<ServiceRecord> GetServices();

    /// <summary>Every account with the User role.</summary>
    List<UserRecord> GetPatients();
}

/// <summary>Final project: reports read straight from QueueEntry, Queue, Service, UserCredentials and UserProfile.</summary>
public class SqlReportRepository : IReportRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlReportRepository(IDbConnectionFactory factory) => _factory = factory;

    public List<VisitRecord> GetVisits(DateTime? fromUtc, DateTime? toUtcExclusive, string? serviceName)
    {
        var sql = @"SELECT e.EntryId, e.UserId, c.Email,
                           COALESCE(p.FirstName, '') AS FirstName, COALESCE(p.LastName, '') AS LastName,
                           e.DisplayName, s.Name AS ServiceName, e.JoinTime, e.EndedAt, e.Status, e.Priority
                    FROM QueueEntry e
                    JOIN Queue q            ON q.QueueId = e.QueueId
                    JOIN Service s          ON s.ServiceId = q.ServiceId
                    JOIN UserCredentials c  ON c.UserId = e.UserId
                    LEFT JOIN UserProfile p ON p.UserId = e.UserId
                    WHERE 1 = 1 ";
        if (fromUtc is not null) sql += "AND e.JoinTime >= @fromUtc ";
        if (toUtcExclusive is not null) sql += "AND e.JoinTime < @toUtcExclusive ";
        if (!string.IsNullOrWhiteSpace(serviceName)) sql += "AND LOWER(s.Name) = LOWER(@serviceName) ";
        sql += "ORDER BY e.JoinTime, e.EntryId";

        using var db = _factory.Open();
        return db.Query<VisitRecord>(sql, new { fromUtc, toUtcExclusive, serviceName = serviceName?.Trim() }).ToList();
    }

    public List<ServiceRecord> GetServices()
    {
        using var db = _factory.Open();
        return db.Query<ServiceRecord>(
            @"SELECT s.ServiceId, s.Name, s.Description, s.DurationMinutes, s.Priority,
                     (SELECT q.Status FROM Queue q WHERE q.QueueId =
                         (SELECT MAX(q2.QueueId) FROM Queue q2 WHERE q2.ServiceId = s.ServiceId)) AS QueueStatus
              FROM Service s
              ORDER BY s.Name").ToList();
    }

    public List<UserRecord> GetPatients()
    {
        using var db = _factory.Open();
        return db.Query<UserRecord>(
            @"SELECT c.UserId, c.Email, COALESCE(p.FirstName, '') AS FirstName, COALESCE(p.LastName, '') AS LastName
              FROM UserCredentials c
              LEFT JOIN UserProfile p ON p.UserId = c.UserId
              WHERE c.Role = 'User'
              ORDER BY c.UserId").ToList();
    }
}
