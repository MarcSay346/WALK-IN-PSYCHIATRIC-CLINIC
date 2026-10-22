namespace ClinicApi.Models;

public enum NotificationType
{
    General,
    JoinedQueue,
    AlmostReady
}

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Message { get; set; } = "";
    public NotificationType Type { get; set; } = NotificationType.General;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public record CreateNotificationRequest(int UserId, string? Message);
