using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Everything about bookable resources.
///
/// Reading resources: any logged-in user.
/// Creating / editing / deactivating: Admin only  ->  Role-Based Access Control.
/// </summary>
[ApiController]
[Route("api/resources")]
[Authorize]
public class ResourcesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ResourcesController(AppDbContext db) => _db = db;

    /// <summary>
    /// Lists resources. Students and faculty see only active ones;
    /// the admin can ask for all of them with ?includeInactive=true.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ResourceDto>>> GetAll([FromQuery] bool includeInactive = false)
    {
        var isAdmin = User.IsInRole(nameof(UserRole.Admin));
        var query = _db.Resources.AsQueryable();

        if (!isAdmin || !includeInactive)
            query = query.Where(r => r.IsActive);

        var resources = await query
            .OrderBy(r => r.Type).ThenBy(r => r.Name)
            .Select(r => ToDto(r))
            .ToListAsync();

        return Ok(resources);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ResourceDto>> GetById(int id)
    {
        var resource = await _db.Resources.FindAsync(id);
        if (resource is null)
            return NotFound(new { message = "Resource not found." });

        return Ok(ToDto(resource));
    }

    /// <summary>Admin only: add a new resource.</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ResourceDto>> Create(ResourceRequest request)
    {
        var resource = new Resource
        {
            Name = request.Name.Trim(),
            Type = request.Type.Trim(),
            Location = request.Location.Trim(),
            Capacity = request.Capacity,
            Description = request.Description?.Trim() ?? string.Empty,
            ImageUrl = request.ImageUrl?.Trim() ?? string.Empty,
            IsActive = request.IsActive
        };

        _db.Resources.Add(resource);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = resource.Id }, ToDto(resource));
    }

    /// <summary>Admin only: edit an existing resource.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<ResourceDto>> Update(int id, ResourceRequest request)
    {
        var resource = await _db.Resources.FindAsync(id);
        if (resource is null)
            return NotFound(new { message = "Resource not found." });

        resource.Name = request.Name.Trim();
        resource.Type = request.Type.Trim();
        resource.Location = request.Location.Trim();
        resource.Capacity = request.Capacity;
        resource.Description = request.Description?.Trim() ?? string.Empty;
        resource.ImageUrl = request.ImageUrl?.Trim() ?? string.Empty;
        resource.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return Ok(ToDto(resource));
    }

    /// <summary>
    /// Admin only: remove a resource.
    ///
    /// If the resource has never been booked it is really deleted.
    /// If it already has bookings we keep the row and only set IsActive = false,
    /// so the booking history stays correct.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult> Delete(int id)
    {
        var resource = await _db.Resources.FindAsync(id);
        if (resource is null)
            return NotFound(new { message = "Resource not found." });

        var hasBookings = await _db.Bookings.AnyAsync(b => b.ResourceId == id);

        if (hasBookings)
        {
            resource.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { deactivated = true, message = $"'{resource.Name}' has existing bookings, so it was deactivated instead of deleted." });
        }

        _db.Resources.Remove(resource);
        await _db.SaveChangesAsync();
        return Ok(new { deactivated = false, message = $"'{resource.Name}' was deleted." });
    }

    /// <summary>Admin only: how often each resource is used.</summary>
    [HttpGet("/api/reports/utilization")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<List<UtilizationDto>>> Utilization()
    {
        // The whole report is small (a few hundred rows in a college project),
        // so we load the data and group it in memory - easy to read and to explain.
        var resources = await _db.Resources
            .OrderBy(r => r.Name)
            .Select(r => new { r.Id, r.Name, r.Type })
            .ToListAsync();

        var bookings = await _db.Bookings
            .Select(b => new { b.ResourceId, b.Status, b.StartTime, b.EndTime })
            .ToListAsync();

        var totalBookings = bookings.Count;

        var report = resources
            .Select(r =>
            {
                var mine = bookings.Where(b => b.ResourceId == r.Id).ToList();
                var approved = mine.Where(b => b.Status == BookingStatus.Approved).ToList();
                var minutes = approved.Sum(b => (b.EndTime - b.StartTime).TotalMinutes);

                return new UtilizationDto
                {
                    ResourceId = r.Id,
                    ResourceName = r.Name,
                    Type = r.Type,
                    TotalBookings = mine.Count,
                    ApprovedBookings = approved.Count,
                    PendingBookings = mine.Count(b => b.Status == BookingStatus.Pending),
                    BookedHours = Math.Round(minutes / 60.0, 1),
                    SharePercent = totalBookings == 0 ? 0 : Math.Round(mine.Count * 100.0 / totalBookings, 1)
                };
            })
            .OrderByDescending(r => r.TotalBookings)
            .ThenBy(r => r.ResourceName)
            .ToList();

        return Ok(report);
    }

    private static ResourceDto ToDto(Resource r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Type = r.Type,
        Location = r.Location,
        Capacity = r.Capacity,
        Description = r.Description,
        ImageUrl = r.ImageUrl,
        IsActive = r.IsActive,
        CreatedAt = r.CreatedAt
    };
}
