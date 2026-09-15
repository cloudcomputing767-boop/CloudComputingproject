namespace Backend.Models;

/// <summary>
/// A request from one user to use one resource on one date, between two times.
/// </summary>
public class Booking
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ResourceId { get; set; }
    public Resource Resource { get; set; } = null!;

    /// <summary>The calendar day of the booking (no time part).</summary>
    public DateOnly BookingDate { get; set; }

    /// <summary>Start time of day, e.g. 10:00.</summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>End time of day, e.g. 12:00. Must be later than StartTime.</summary>
    public TimeOnly EndTime { get; set; }

    public string Purpose { get; set; } = string.Empty;

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
