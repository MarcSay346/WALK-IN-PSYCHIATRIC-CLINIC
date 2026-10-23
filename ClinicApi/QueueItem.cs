using System;

namespace RadiantMindClinic.Models
{
    public class QueueItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string Name { get; set; }
        public string Contact { get; set; }
        public string ServiceName { get; set; } // e.g., "Dr. Nate", "Dr. Valdez", "Dr. Cole"
        public int Priority { get; set; }        // Lower number = Higher priority (1 = Urgent, 2 = High, 3 = Standard)
        public DateTime ArrivalTime { get; set; } = DateTime.UtcNow;
        public int EstimatedWaitMinutes { get; set; }
        public int Position { get; set; }
    }

    public class JoinQueueRequest
    {
        public string Name { get; set; }
        public string Contact { get; set; }
        public string ServiceName { get; set; }
        public int Priority { get; set; } = 3; // Default to standard priority
    }
}
