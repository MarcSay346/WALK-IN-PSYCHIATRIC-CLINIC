using System;
using System.Collections.Generic;
using System.Linq;
using RadiantMindClinic.Models;

namespace RadiantMindClinic.Services
{
    public interface IQueueService
    {
        IEnumerable<QueueItem> GetCurrentQueue(string serviceName = null);
        QueueItem JoinQueue(JoinQueueRequest request);
        bool LeaveQueue(string id);
        QueueItem ServeNext(string serviceName = null);
        QueueItem GetPatientStatus(string id);
    }

    public class QueueService : IQueueService
    {
        private readonly List<QueueItem> _queue = new();
        private readonly object _lock = new();

        // Rule-based base expected durations per service/provider (in minutes)
        private readonly Dictionary<string, int> _serviceDurations = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Dr. Nate", 60 },
            { "Dr. Valdez", 45 },
            { "Dr. Cole", 30 },
            { "Urgent Consult", 10 },
            { "General Walk-In Consultation", 20 }
        };

        public IEnumerable<QueueItem> GetCurrentQueue(string serviceName = null)
        {
            lock (_lock)
            {
                RecalculateQueueMetrics();
                var query = _queue.AsEnumerable();
                
                if (!string.IsNullOrWhiteSpace(serviceName))
                {
                    query = query.Where(q => q.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
                }

                return query.OrderBy(q => q.Priority).ThenBy(q => q.ArrivalTime).ToList();
            }
        }

        public QueueItem JoinQueue(JoinQueueRequest request)
        {
            lock (_lock)
            {
                var newItem = new QueueItem
                {
                    Name = request.Name,
                    Contact = request.Contact,
                    ServiceName = request.ServiceName ?? "General Walk-In Consultation",
                    Priority = request.Priority,
                    ArrivalTime = DateTime.UtcNow
                };

                _queue.Add(newItem);
                RecalculateQueueMetrics();
                return newItem;
            }
        }

        public bool LeaveQueue(string id)
        {
            lock (_lock)
            {
                var item = _queue.FirstOrDefault(q => q.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
                if (item == null) return false;

                _queue.Remove(item);
                RecalculateQueueMetrics();
                return true;
            }
        }

        public QueueItem ServeNext(string serviceName = null)
        {
            lock (_lock)
            {
                var query = _queue.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(serviceName))
                {
                    query = query.Where(q => q.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
                }

                var nextPatient = query.OrderBy(q => q.Priority).ThenBy(q => q.ArrivalTime).FirstOrDefault();
                if (nextPatient == null) return null;

                _queue.Remove(nextPatient);
                RecalculateQueueMetrics();
                return nextPatient;
            }
        }

        public QueueItem GetPatientStatus(string id)
        {
            lock (_lock)
            {
                RecalculateQueueMetrics();
                return _queue.FirstOrDefault(q => q.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void RecalculateQueueMetrics()
        {
            // Group by service to calculate independent queue positions and wait times per service line
            var serviceGroups = _queue.GroupBy(q => q.ServiceName);

            foreach (var group in serviceGroups)
            {
                var sortedGroup = group.OrderBy(q => q.Priority).ThenBy(q => q.ArrivalTime).ToList();
                int cumulativeWait = 0;

                for (int i = 0; i < sortedGroup.Count; i++)
                {
                    var item = sortedGroup[i];
                    item.Position = i + 1;

                    // Rule-based wait estimation: Position / cumulative expected duration
                    int duration = _serviceDurations.TryGetValue(item.ServiceName, out var d) ? d : 20;
                    item.EstimatedWaitMinutes = cumulativeWait;
                    cumulativeWait += duration;
                }
            }
        }
    }
}
