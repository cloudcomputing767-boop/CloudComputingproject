using System.Security.Claims;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// The booking workflow:
/// user checks availability -> creates a Pending booking -> admin approves or rejects.
/// </summary>
[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly BookingService _bookings;

    public BookingsController(AppDbContext db, BookingService bookings)
    {
        _db = db;
        _bookings = bookings;
    }

    /// <summary>The id of the logged-in user, read from the JWT token.</summary>
    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    // ------------------------------------------------------------------
    // Availability
    // ------------------------------------------------------------------

    /// <summary>
    /// GET /api/bookings/availability?resourceId=3&amp;date=2026-09-20&amp;startTime=10:00&amp;endTime=12:00
    /// Returns { "available": true|false, ... }
    /// </summary>
    [HttpGet("availability")]
    public async Task<ActionResult<AvailabilityResponse>> CheckAvailability(
        [FromQuery] int resourceId,
        [FromQuery] DateOnly date,
        [FromQuery] TimeOnly startTime,
        [FromQuery] TimeOnly endTime)
    {
        var result = await _bookings.CheckAvailabilityAsync(resourceId, date, startTime, endTime);
        return Ok(result);
    }

    /// <summary>Time slots already taken for one resource on one day.</summary>
    [HttpGet("busy")]
    public async Task<ActionResult<List<BusySlot>>> BusySlots(
        [FromQuery] int resourceId,
        [FromQuery] DateOnly date)
    {
        return Ok(await _bookings.GetBusySlotsAsync(resourceId, date));
    }

    // ------------------------------------------------------------------
    // Creating a booking
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates a Pending booking - but only after the server has checked
    /// availability again. The frontend check is only a convenience; THIS is
    /// the check that actually prevents double booking.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create(BookingRequest request)
    {
        if (request.StartTime >= request.EndTime)
            return BadRequest(new { message = "Start time must be before end time." });

        var resource = await _db.Resources.FindAsync(request.ResourceId);
        if (resource is null)
            return BadRequest(new { message = "The selected resource does not exist." });

        if (!resource.IsActive)
            return BadRequest(new { message = "This resource is currently unavailable." });

        if (request.BookingDate < DateOnly.FromDateTime(DateTime.UtcNow.Date))
            return BadRequest(new { message = "You cannot book a date in the past." });

        var userId = CurrentUserId;

        // Does the SAME user already hold an overlapping booking for this resource?
        var conflicts = await _bookings.FindConflictsAsync(
            request.ResourceId, request.BookingDate, request.StartTime, request.EndTime);

        if (conflicts.Any(c => c.UserId == userId))
            return Conflict(new { message = "You already have a booking for this resource at this time." });

        if (conflicts.Count > 0)
            return Conflict(new { message = $"{resource.Name} is not available during this time. It is already booked." });

        var booking = new Booking
        {
            UserId = userId,
            ResourceId = request.ResourceId,
            BookingDate = request.BookingDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Purpose = request.Purpose.Trim(),
            Status = BookingStatus.Pending
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        await _db.Entry(booking).Reference(b => b.Resource).LoadAsync();
        await _db.Entry(booking).Reference(b => b.User).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, BookingService.ToDto(booking));
    }

    // ------------------------------------------------------------------
    // Reading bookings
    // ------------------------------------------------------------------

    /// <summary>The bookings of the logged-in user ("My Bookings").</summary>
    [HttpGet("my")]
    public async Task<ActionResult<List<BookingDto>>> MyBookings()
    {
        var userId = CurrentUserId;

        var list = await _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.BookingDate).ThenByDescending(b => b.StartTime)
            .ToListAsync();

        return Ok(list.Select(BookingService.ToDto).ToList());
    }

    /// <summary>Admin only: every booking in the system, newest first.</summary>
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<List<BookingDto>>> GetAll([FromQuery] string? status = null)
    {
        var query = _db.Bookings.Include(b => b.Resource).Include(b => b.User).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(b => b.Status == parsed);
        }

        var list = await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return Ok(list.Select(BookingService.ToDto).ToList());
    }

    /// <summary>One booking. Users can only read their own; the admin can read any.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingDto>> GetById(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null)
            return NotFound(new { message = "Booking not found." });

        if (booking.UserId != CurrentUserId && !IsAdmin)
            return Forbid();

        return Ok(BookingService.ToDto(booking));
    }

    /// <summary>Counters for the dashboards. Users see their own, the admin sees the whole system.</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<BookingStatsDto>> Stats()
    {
        var query = _db.Bookings.AsQueryable();

        if (!IsAdmin)
        {
            var userId = CurrentUserId;
            query = query.Where(b => b.UserId == userId);
        }

        var stats = new BookingStatsDto
        {
            Total = await query.CountAsync(),
            Pending = await query.CountAsync(b => b.Status == BookingStatus.Pending),
            Approved = await query.CountAsync(b => b.Status == BookingStatus.Approved),
            Rejected = await query.CountAsync(b => b.Status == BookingStatus.Rejected),
            Cancelled = await query.CountAsync(b => b.Status == BookingStatus.Cancelled),
            TotalResources = await _db.Resources.CountAsync(r => r.IsActive)
        };

        return Ok(stats);
    }

    // ------------------------------------------------------------------
    // Changing the status
    // ------------------------------------------------------------------

    /// <summary>Admin only: approve a pending request.</summary>
    [HttpPut("{id:int}/approve")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<BookingDto>> Approve(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null)
            return NotFound(new { message = "Booking not found." });

        if (booking.Status != BookingStatus.Pending)
            return BadRequest(new { message = $"Only pending bookings can be approved. This one is {booking.Status}." });

        // Safety check: while this request was waiting, another booking for the
        // same slot may already have been approved.
        var conflicts = await _bookings.FindConflictsAsync(
            booking.ResourceId, booking.BookingDate, booking.StartTime, booking.EndTime, ignoreBookingId: booking.Id);

        if (conflicts.Any(c => c.Status == BookingStatus.Approved))
            return Conflict(new { message = "Another booking for this time slot has already been approved." });

        booking.Status = BookingStatus.Approved;
        await _db.SaveChangesAsync();

        return Ok(BookingService.ToDto(booking));
    }

    /// <summary>Admin only: reject a pending request. The time slot becomes free again.</summary>
    [HttpPut("{id:int}/reject")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<BookingDto>> Reject(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null)
            return NotFound(new { message = "Booking not found." });

        if (booking.Status is BookingStatus.Rejected or BookingStatus.Cancelled)
            return BadRequest(new { message = $"This booking is already {booking.Status}." });

        booking.Status = BookingStatus.Rejected;
        await _db.SaveChangesAsync();

        return Ok(BookingService.ToDto(booking));
    }

    /// <summary>
    /// The owner of a booking cancels it (or the admin cancels it for them).
    /// A cancelled booking no longer blocks the resource.
    /// </summary>
    [HttpPut("{id:int}/cancel")]
    public async Task<ActionResult<BookingDto>> Cancel(int id)
    {
        var booking = await _db.Bookings
            .Include(b => b.Resource)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking is null)
            return NotFound(new { message = "Booking not found." });

        if (booking.UserId != CurrentUserId && !IsAdmin)
            return Forbid();

        if (booking.Status is BookingStatus.Rejected or BookingStatus.Cancelled)
            return BadRequest(new { message = $"This booking is already {booking.Status}." });

        booking.Status = BookingStatus.Cancelled;
        await _db.SaveChangesAsync();

        return Ok(BookingService.ToDto(booking));
    }
}
