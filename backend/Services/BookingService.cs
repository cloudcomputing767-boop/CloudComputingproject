using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>
/// Contains the most important rule of the whole system:
/// a resource can never be booked twice for overlapping times.
///
/// The check runs on the SERVER, against the cloud database, so it is applied
/// even if somebody bypasses the web page and calls the API directly.
/// </summary>
public class BookingService
{
    private readonly AppDbContext _db;

    public BookingService(AppDbContext db) => _db = db;

    /// <summary>
    /// Only these two statuses reserve the resource.
    /// Rejected and Cancelled bookings free the slot again.
    /// </summary>
    private static readonly BookingStatus[] BlockingStatuses =
    {
        BookingStatus.Pending,
        BookingStatus.Approved
    };

    /// <summary>
    /// Finds every booking that overlaps the requested time window.
    ///
    /// Two time ranges overlap when:
    ///     existing.StartTime &lt; requested.EndTime
    ///     AND existing.EndTime &gt; requested.StartTime
    ///
    /// Example - existing booking 10:00-12:00 on the same resource and date:
    ///     11:00-13:00  ->  overlaps  (10:00 &lt; 13:00  and  12:00 &gt; 11:00)
    ///     12:00-14:00  ->  free      (12:00 &gt; 11:00 is true, but 10:00 &lt; 14:00
    ///                                 and 12:00 &gt; 12:00 is FALSE, so no overlap)
    ///     08:00-10:00  ->  free      (back-to-back bookings are allowed)
    /// </summary>
    public async Task<List<Booking>> FindConflictsAsync(
        int resourceId,
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        int? ignoreBookingId = null)
    {
        var query = _db.Bookings
            .Where(b => b.ResourceId == resourceId
                        && b.BookingDate == date
                        && BlockingStatuses.Contains(b.Status)
                        && b.StartTime < end
                        && b.EndTime > start);

        if (ignoreBookingId.HasValue)
            query = query.Where(b => b.Id != ignoreBookingId.Value);

        return await query.OrderBy(b => b.StartTime).ToListAsync();
    }

    /// <summary>
    /// Runs the availability check and builds the answer sent to the browser.
    /// </summary>
    public async Task<AvailabilityResponse> CheckAvailabilityAsync(
        int resourceId, DateOnly date, TimeOnly start, TimeOnly end)
    {
        // --- basic validation first ---
        if (start >= end)
        {
            return new AvailabilityResponse
            {
                Available = false,
                Message = "Start time must be before end time."
            };
        }

        var resource = await _db.Resources.FindAsync(resourceId);
        if (resource is null)
            return new AvailabilityResponse { Available = false, Message = "Resource not found." };

        if (!resource.IsActive)
            return new AvailabilityResponse { Available = false, Message = "This resource is currently unavailable." };

        if (date < DateOnly.FromDateTime(DateTime.UtcNow.Date))
            return new AvailabilityResponse { Available = false, Message = "You cannot book a date in the past." };

        // --- the overlap check against the database ---
        var conflicts = await FindConflictsAsync(resourceId, date, start, end);

        if (conflicts.Count == 0)
        {
            return new AvailabilityResponse
            {
                Available = true,
                Message = $"{resource.Name} is available on {date:dd/MM/yyyy} from {start:HH\\:mm} to {end:HH\\:mm}."
            };
        }

        return new AvailabilityResponse
        {
            Available = false,
            Message = $"{resource.Name} is not available during this time. It is already booked.",
            ConflictingSlots = conflicts.Select(c => new BusySlot
            {
                StartTime = c.StartTime,
                EndTime = c.EndTime,
                Status = c.Status.ToString()
            }).ToList()
        };
    }

    /// <summary>All bookings that already occupy a resource on a given day.</summary>
    public async Task<List<BusySlot>> GetBusySlotsAsync(int resourceId, DateOnly date)
    {
        return await _db.Bookings
            .Where(b => b.ResourceId == resourceId
                        && b.BookingDate == date
                        && BlockingStatuses.Contains(b.Status))
            .OrderBy(b => b.StartTime)
            .Select(b => new BusySlot
            {
                StartTime = b.StartTime,
                EndTime = b.EndTime,
                Status = b.Status.ToString()
            })
            .ToListAsync();
    }

    /// <summary>Converts a database entity into the shape the API returns.</summary>
    public static BookingDto ToDto(Booking b) => new()
    {
        Id = b.Id,
        ResourceId = b.ResourceId,
        ResourceName = b.Resource?.Name ?? string.Empty,
        ResourceLocation = b.Resource?.Location ?? string.Empty,
        UserId = b.UserId,
        UserName = b.User?.FullName ?? string.Empty,
        UserRole = b.User?.Role.ToString() ?? string.Empty,
        BookingDate = b.BookingDate,
        StartTime = b.StartTime,
        EndTime = b.EndTime,
        Purpose = b.Purpose,
        Status = b.Status.ToString(),
        CreatedAt = b.CreatedAt
    };
}
