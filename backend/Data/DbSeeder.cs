using Backend.Models;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

/// <summary>
/// Puts demo data into the database the first time the API starts, so the
/// project can be demonstrated immediately (in class, or right after it is
/// deployed to the cloud).
///
/// It only inserts rows that are missing, so restarting the API never creates
/// duplicates.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, IConfiguration config, PasswordHasher hasher)
    {
        // ---------- 1. The admin account ----------
        // Credentials come from configuration (appsettings / environment
        // variables) so that the production password is never written in code.
        var adminEmail = (config["Seed:AdminEmail"] ?? "admin@college.com").Trim().ToLowerInvariant();
        var adminPassword = config["Seed:AdminPassword"] ?? "Admin123!";
        var adminName = config["Seed:AdminName"] ?? "System Administrator";

        if (!await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            db.Users.Add(new User
            {
                FullName = adminName,
                Email = adminEmail,
                PasswordHash = hasher.Hash(adminPassword),
                Role = UserRole.Admin
            });
        }

        // ---------- 2. Demo student / faculty accounts ----------
        var demoUsers = new[]
        {
            new { Name = "Ahmed Hassan",  Email = "student@college.com", Role = UserRole.Student },
            new { Name = "Dr. Sara Ali",  Email = "faculty@college.com", Role = UserRole.Faculty }
        };

        foreach (var demo in demoUsers)
        {
            if (!await db.Users.AnyAsync(u => u.Email == demo.Email))
            {
                db.Users.Add(new User
                {
                    FullName = demo.Name,
                    Email = demo.Email,
                    PasswordHash = hasher.Hash("Demo123!"),
                    Role = demo.Role
                });
            }
        }

        // ---------- 3. Sample resources ----------
        // Each resource gets its own picture. We store only the path; the image
        // files themselves live in frontend/images/resources/.
        var sampleResources = new List<Resource>
        {
            new() { Name = "Classroom 101",    Type = "Classroom",       Location = "Main Building",          Capacity = 60,  Description = "Standard classroom with whiteboard and projector.", ImageUrl = "images/resources/classroom.svg" },
            new() { Name = "Classroom 102",    Type = "Classroom",       Location = "Main Building",          Capacity = 45,  Description = "Classroom with air conditioning.",                  ImageUrl = "images/resources/classroom-2.svg" },
            new() { Name = "Computer Lab 1",   Type = "Computer Lab",    Location = "Computer Science Block", Capacity = 40,  Description = "40 desktop computers with development software.",   ImageUrl = "images/resources/computer-lab.svg" },
            new() { Name = "Computer Lab 2",   Type = "Computer Lab",    Location = "Computer Science Block", Capacity = 30,  Description = "Networking and hardware lab.",                      ImageUrl = "images/resources/computer-lab-2.svg" },
            new() { Name = "Seminar Hall",     Type = "Seminar Hall",    Location = "Main Block",             Capacity = 150, Description = "Large hall with stage and sound system.",            ImageUrl = "images/resources/seminar-hall.svg" },
            new() { Name = "Projector 1",      Type = "Projector",       Location = "Equipment Store",        Capacity = 1,   Description = "Portable HD projector that can be borrowed.",        ImageUrl = "images/resources/projector.svg" },
            new() { Name = "Meeting Room A",   Type = "Meeting Room",    Location = "Administration Block",   Capacity = 12,  Description = "Small meeting room with conference table.",          ImageUrl = "images/resources/meeting-room.svg" },
            new() { Name = "Basketball Court", Type = "Sports Facility", Location = "Sports Complex",         Capacity = 30,  Description = "Outdoor basketball court.",                         ImageUrl = "images/resources/sports.svg" }
        };

        foreach (var resource in sampleResources)
        {
            var existing = await db.Resources.FirstOrDefaultAsync(r => r.Name == resource.Name);

            if (existing is null)
            {
                db.Resources.Add(resource);
            }
            else if (string.IsNullOrWhiteSpace(existing.ImageUrl))
            {
                // The database was created before pictures existed - fill it in.
                existing.ImageUrl = resource.ImageUrl;
            }
        }

        await db.SaveChangesAsync();
    }
}
