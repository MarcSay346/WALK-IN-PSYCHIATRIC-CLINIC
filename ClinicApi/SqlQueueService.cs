using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ClinicApi.Data;
using ClinicApi.Services;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using RadiantMindClinic.Models;

namespace RadiantMindClinic.Services
{
    /// <summary>
    /// A4 Queue module stored in the database (Queue + QueueEntry tables).
    /// Same rules as the in-memory QueueService: each service has its own queue, ordered by
    /// priority (1 = Urgent first) and then arrival time. The wait estimate uses the
    /// service's expected duration from the Service table.
    /// </summary>
    public class SqlQueueService : IQueueService
    {
        public const int MaxNameLength = 100;
        public const int MaxContactLength = 100;

        private readonly IDbConnectionFactory _factory;
        private readonly object _lock = new();

        public SqlQueueService(IDbConnectionFactory factory) => _factory = factory;

        private class EntryRow
        {
            public int EntryId { get; set; }
            public int UserId { get; set; }
            public int QueueId { get; set; }
            public string DisplayName { get; set; } = "";
            public string? Contact { get; set; }
            public int Priority { get; set; }
            public DateTime JoinTime { get; set; }
            public string ServiceName { get; set; } = "";
            public int DurationMinutes { get; set; }
        }

        private const string SelectWaiting =
            @"SELECT e.EntryId, e.UserId, e.QueueId, e.DisplayName, e.Contact, e.Priority, e.JoinTime,
                     s.Name AS ServiceName, s.DurationMinutes
              FROM QueueEntry e
              JOIN Queue q   ON q.QueueId = e.QueueId
              JOIN Service s ON s.ServiceId = q.ServiceId
              WHERE e.Status = 'Waiting' ";

        // ---------- IQueueService ----------

        public IEnumerable<QueueItem> GetCurrentQueue(string serviceName = null)
        {
            using var db = _factory.Open();
            return Ordered(BuildItems(LoadWaiting(db, null, serviceName)));
        }

        public QueueItem JoinQueue(JoinQueueRequest request)
        {
            Validate(request);
            var serviceName = request.ServiceName!.Trim();

            lock (_lock)
            {
                using var db = _factory.Open();
                using var tx = db.BeginTransaction();

                var serviceId = db.ExecuteScalar<int?>(
                    "SELECT ServiceId FROM Service WHERE LOWER(Name) = LOWER(@serviceName)", new { serviceName }, tx)
                    ?? throw new ApiValidationException("serviceName", $"There is no service named '{serviceName}'.");

                var queueId = OpenQueueFor(db, tx, serviceId, serviceName);

                var userId = DbUsers.TryResolve(db, request.UserId!, tx)
                    ?? throw new ApiValidationException("userId", "Please log in again before joining a queue.");

                var alreadyWaiting = db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM QueueEntry WHERE QueueId = @queueId AND UserId = @userId AND Status = 'Waiting'",
                    new { queueId, userId }, tx);
                if (alreadyWaiting > 0)
                    throw new InvalidOperationException($"You are already in the queue for {serviceName}.");

                var waiting = db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM QueueEntry WHERE QueueId = @queueId AND Status = 'Waiting'", new { queueId }, tx);

                var entryId = db.ExecuteScalar<int>(
                    @"INSERT INTO QueueEntry (QueueId, UserId, DisplayName, Contact, Priority, Position, JoinTime, Status)
                      VALUES (@queueId, @userId, @name, @contact, @priority, @position, @joinTime, 'Waiting');" + _factory.SelectLastId,
                    new
                    {
                        queueId, userId,
                        name = request.Name.Trim(),
                        contact = request.Contact?.Trim(),
                        priority = request.Priority,
                        position = waiting + 1,
                        joinTime = DateTime.UtcNow
                    }, tx);

                var items = SavePositions(db, tx, queueId);
                tx.Commit();
                return items.First(i => i.Id == entryId.ToString());
            }
        }

        public bool LeaveQueue(string id) => EndEntry(id, "Canceled");

        public QueueItem ServeNext(string serviceName = null)
        {
            lock (_lock)
            {
                using var db = _factory.Open();
                using var tx = db.BeginTransaction();

                var next = Ordered(BuildItems(LoadWaiting(db, tx, serviceName))).FirstOrDefault();
                if (next is null) return null;

                var entryId = int.Parse(next.Id);
                db.Execute("UPDATE QueueEntry SET Status = 'Served', EndedAt = @now WHERE EntryId = @entryId",
                    new { now = DateTime.UtcNow, entryId }, tx);
                var queueId = db.ExecuteScalar<int>("SELECT QueueId FROM QueueEntry WHERE EntryId = @entryId", new { entryId }, tx);
                SavePositions(db, tx, queueId);
                tx.Commit();
                return next;
            }
        }

        public QueueItem GetPatientStatus(string id)
        {
            if (!int.TryParse(id, out var entryId)) return null;

            using var db = _factory.Open();
            var queueId = db.ExecuteScalar<int?>(
                "SELECT QueueId FROM QueueEntry WHERE EntryId = @entryId AND Status = 'Waiting'", new { entryId });
            if (queueId is null) return null;

            return BuildItems(LoadWaiting(db, null, null, queueId)).FirstOrDefault(i => i.Id == id);
        }

        // ---------- extra (A4): open / close a service's queue ----------

        /// <summary>Opens or closes the queue for a service. Closed queues don't accept new people.</summary>
        public string SetQueueStatus(string serviceName, bool open)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ApiValidationException("serviceName", "Service name is required.");

            lock (_lock)
            {
                using var db = _factory.Open();
                var serviceId = db.ExecuteScalar<int?>(
                    "SELECT ServiceId FROM Service WHERE LOWER(Name) = LOWER(@serviceName)", new { serviceName = serviceName.Trim() })
                    ?? throw new KeyNotFoundException($"There is no service named '{serviceName.Trim()}'.");

                var queueId = db.ExecuteScalar<int?>(
                    "SELECT MAX(QueueId) FROM Queue WHERE ServiceId = @serviceId", new { serviceId });
                if (queueId is null)
                {
                    db.Execute("INSERT INTO Queue (ServiceId, Status, CreatedAt) VALUES (@serviceId, @status, @now)",
                        new { serviceId, status = open ? "Open" : "Closed", now = DateTime.UtcNow });
                }
                else
                {
                    db.Execute("UPDATE Queue SET Status = @status WHERE QueueId = @queueId",
                        new { status = open ? "Open" : "Closed", queueId });
                }
                return open ? "Open" : "Closed";
            }
        }

        public string GetQueueStatus(string serviceName)
        {
            using var db = _factory.Open();
            var status = db.ExecuteScalar<string?>(
                @"SELECT q.Status FROM Queue q JOIN Service s ON s.ServiceId = q.ServiceId
                  WHERE LOWER(s.Name) = LOWER(@serviceName) ORDER BY q.QueueId DESC",
                new { serviceName = serviceName?.Trim() ?? "" });
            return status ?? "Open";   // a service with no queue yet gets one opened on the first join
        }

        // ---------- helpers ----------

        private static void Validate(JoinQueueRequest? request)
        {
            var errors = new Dictionary<string, string>();
            if (request is null)
                throw new ApiValidationException("body", "Request body is required.");

            InputRules.RequiredText(errors, "name", "Name", request.Name, MaxNameLength);
            if ((request.Contact?.Trim().Length ?? 0) > MaxContactLength)
                errors["contact"] = $"Contact must be {MaxContactLength} characters or fewer.";
            InputRules.RequiredText(errors, "serviceName", "Service", request.ServiceName, 100);
            InputRules.IntInRange(errors, "priority", "Priority", request.Priority, 1, 3);
            if (string.IsNullOrWhiteSpace(request.UserId))
                errors["userId"] = "Please log in before joining a queue.";
            InputRules.ThrowIfInvalid(errors);
        }

        /// <summary>The open queue for a service; creates one if the service never had a queue.</summary>
        private int OpenQueueFor(IDbConnection db, IDbTransaction tx, int serviceId, string serviceName)
        {
            var open = db.ExecuteScalar<int?>(
                "SELECT QueueId FROM Queue WHERE ServiceId = @serviceId AND Status = 'Open'", new { serviceId }, tx);
            if (open is not null) return open.Value;

            var any = db.ExecuteScalar<int>("SELECT COUNT(*) FROM Queue WHERE ServiceId = @serviceId", new { serviceId }, tx);
            if (any > 0)
                throw new InvalidOperationException($"The queue for {serviceName} is closed right now.");

            return db.ExecuteScalar<int>(
                "INSERT INTO Queue (ServiceId, Status, CreatedAt) VALUES (@serviceId, 'Open', @now);" + _factory.SelectLastId,
                new { serviceId, now = DateTime.UtcNow }, tx);
        }

        private bool EndEntry(string id, string status)
        {
            if (!int.TryParse(id, out var entryId)) return false;

            lock (_lock)
            {
                using var db = _factory.Open();
                using var tx = db.BeginTransaction();
                var changed = db.Execute(
                    "UPDATE QueueEntry SET Status = @status, EndedAt = @now WHERE EntryId = @entryId AND Status = 'Waiting'",
                    new { status, now = DateTime.UtcNow, entryId }, tx);
                if (changed == 0) return false;

                var queueId = db.ExecuteScalar<int>("SELECT QueueId FROM QueueEntry WHERE EntryId = @entryId", new { entryId }, tx);
                SavePositions(db, tx, queueId);
                tx.Commit();
                return true;
            }
        }

        private static List<EntryRow> LoadWaiting(IDbConnection db, IDbTransaction? tx, string? serviceName, int? queueId = null)
        {
            var sql = SelectWaiting;
            if (!string.IsNullOrWhiteSpace(serviceName)) sql += "AND LOWER(s.Name) = LOWER(@serviceName) ";
            if (queueId is not null) sql += "AND e.QueueId = @queueId ";
            return db.Query<EntryRow>(sql, new { serviceName = serviceName?.Trim(), queueId }, tx).ToList();
        }

        /// <summary>Positions and wait estimates, counted separately for each queue (same rule as QueueService).</summary>
        private static List<QueueItem> BuildItems(List<EntryRow> rows)
        {
            var items = new List<QueueItem>();
            foreach (var group in rows.GroupBy(r => r.QueueId))
            {
                var wait = 0;
                var position = 1;
                foreach (var row in group.OrderBy(r => r.Priority).ThenBy(r => r.JoinTime).ThenBy(r => r.EntryId))
                {
                    items.Add(new QueueItem
                    {
                        Id = row.EntryId.ToString(),
                        UserId = row.UserId.ToString(),
                        Name = row.DisplayName,
                        Contact = row.Contact ?? "",
                        ServiceName = row.ServiceName,
                        Priority = row.Priority,
                        ArrivalTime = row.JoinTime,
                        Position = position++,
                        EstimatedWaitMinutes = wait
                    });
                    wait += row.DurationMinutes;
                }
            }
            return items;
        }

        private static List<QueueItem> Ordered(IEnumerable<QueueItem> items) =>
            items.OrderBy(i => i.Priority).ThenBy(i => i.ArrivalTime).ThenBy(i => int.Parse(i.Id)).ToList();

        /// <summary>Recounts positions for one queue and saves them in QueueEntry.Position.</summary>
        private static List<QueueItem> SavePositions(IDbConnection db, IDbTransaction tx, int queueId)
        {
            var items = BuildItems(LoadWaiting(db, tx, null, queueId));
            foreach (var item in items)
            {
                db.Execute("UPDATE QueueEntry SET Position = @Position WHERE EntryId = @entryId",
                    new { item.Position, entryId = int.Parse(item.Id) }, tx);
            }
            return items;
        }
    }

    public static class QueueSetup
    {
        /// <summary>Queue module: SQL Server when a ClinicDb connection string is set, otherwise in memory.</summary>
        public static IServiceCollection AddQueue(this IServiceCollection services)
        {
            services.AddSingleton<IQueueService>(sp => DatabaseSetup.Pick<IQueueService>(sp,
                db => new SqlQueueService(db), () => new QueueService()));
            return services;
        }
    }
}
