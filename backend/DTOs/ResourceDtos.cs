using System.ComponentModel.DataAnnotations;

namespace Backend.DTOs;

public class ResourceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Body used by the admin to create or update a resource.</summary>
public class ResourceRequest
{
    [Required(ErrorMessage = "Name is required.")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Type is required.")]
    [StringLength(60)]
    public string Type { get; set; } = string.Empty;

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(120)]
    public string Location { get; set; } = string.Empty;

    [Range(1, 10000, ErrorMessage = "Capacity must be between 1 and 10000.")]
    public int Capacity { get; set; }

    [StringLength(400)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Path or link to the picture, e.g. "images/resources/classroom.svg".</summary>
    [StringLength(300)]
    public string ImageUrl { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

/// <summary>One row of the admin utilization report.</summary>
public class UtilizationDto
{
    public int ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int TotalBookings { get; set; }
    public int ApprovedBookings { get; set; }
    public int PendingBookings { get; set; }
    /// <summary>Total approved hours booked, e.g. 6.5</summary>
    public double BookedHours { get; set; }
    /// <summary>This resource's share of all bookings in the system, 0-100.</summary>
    public double SharePercent { get; set; }
}
