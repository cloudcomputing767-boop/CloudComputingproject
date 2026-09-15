namespace Backend.Models;

/// <summary>
/// Life cycle of a booking request.
/// Only Pending and Approved bookings occupy a resource;
/// Rejected and Cancelled ones free the time slot again.
/// </summary>
public enum BookingStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}
