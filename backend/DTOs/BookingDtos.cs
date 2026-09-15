using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

/// <summary>Body sent when a student or faculty member requests a booking.</summary>
public class BookingRequest
{
    [Required]
    public int ResourceId { get; set; }

    [Required(ErrorMessage = "Booking date is required.")]
    public DateOnly BookingDate { get; set; }

    [Required(ErrorMessage = "Start time is required.")]
    public TimeOnly StartTime { get; set; }

    [Required(ErrorMessage = "End time is required.")]
    public TimeOnly EndTime { get; set; }

    [Required(ErrorMessage = "Purpose is required.")]
    [StringLength(300, MinimumLength = 3)]
    public string Purpose { get; set; } = string.Empty;
}

public class BookingDto
{
    public int Id { get; set; }
    public int ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceLocation { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public DateOnly BookingDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>Answer of GET /api/bookings/availability.</summary>
public class AvailabilityResponse
{
    public bool Available { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>Time ranges that are already taken on that date, to help the user pick another slot.</summary>
    public List<BusySlot> ConflictingSlots { get; set; } = new();
}

public class BusySlot
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>Small counters shown on the dashboards.</summary>
public class BookingStatsDto
{
    public int Total { get; set; }
    public int Pending { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Cancelled { get; set; }
    public int TotalResources { get; set; }
}
