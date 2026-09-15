namespace Backend.Models;

/// <summary>
/// Something in the college that can be booked: a classroom, lab, hall, projector...
/// </summary>
public class Resource
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Free text, e.g. "Classroom", "Computer Lab", "Seminar Hall".</summary>
    public string Type { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Picture shown on the resource card.
    /// We store only the ADDRESS of the image, not the image itself - databases
    /// are for small pieces of text, not for large files. The file lives in
    /// frontend/images/resources/, so this is normally a short relative path
    /// like "images/resources/computer-lab.svg". A full "https://..." address
    /// works too, which is how a real system would point at cloud storage.
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Instead of deleting a resource that already has bookings, the admin
    /// deactivates it. Inactive resources disappear from the booking screens
    /// but their booking history is kept.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Booking> Bookings { get; set; } = new();
}
