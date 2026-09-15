namespace Backend.Models;

/// <summary>
/// A person who can log in. One user can have many bookings.
/// </summary>
public class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>Unique login name. Stored lower-cased so logins are case-insensitive.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash of the password. The plain password is never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Student;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Booking> Bookings { get; set; } = new();
}
