using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data;

/// <summary>
/// The Entity Framework Core database context.
/// It maps our C# classes to PostgreSQL tables and is the single place
/// where the application talks to the database.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<UploadedImage> UploadedImages => Set<UploadedImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- User ----------
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(u => u.FullName).HasMaxLength(120).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(160).IsRequired();
            entity.Property(u => u.PasswordHash).IsRequired();

            // Two accounts can never share the same email address.
            entity.HasIndex(u => u.Email).IsUnique();

            // Store the enum as readable text ("Student") instead of a number.
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        // ---------- Resource ----------
        modelBuilder.Entity<Resource>(entity =>
        {
            entity.Property(r => r.Name).HasMaxLength(120).IsRequired();
            entity.Property(r => r.Type).HasMaxLength(60).IsRequired();
            entity.Property(r => r.Location).HasMaxLength(120).IsRequired();
            entity.Property(r => r.Description).HasMaxLength(400);
            entity.Property(r => r.ImageUrl).HasMaxLength(300);
        });

        // ---------- UploadedImage ----------
        modelBuilder.Entity<UploadedImage>(entity =>
        {
            entity.Property(i => i.FileName).HasMaxLength(200).IsRequired();
            entity.Property(i => i.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(i => i.Data).IsRequired();
        });

        // ---------- Booking ----------
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(b => b.Purpose).HasMaxLength(300).IsRequired();
            entity.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);

            // One User -> many Bookings
            entity.HasOne(b => b.User)
                  .WithMany(u => u.Bookings)
                  .HasForeignKey(b => b.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // One Resource -> many Bookings.
            // Restrict: a resource that still has bookings cannot be deleted by
            // accident - the admin deactivates it instead (IsActive = false).
            entity.HasOne(b => b.Resource)
                  .WithMany(r => r.Bookings)
                  .HasForeignKey(b => b.ResourceId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Makes the availability query (same resource + same day) fast.
            entity.HasIndex(b => new { b.ResourceId, b.BookingDate });
        });
    }
}
