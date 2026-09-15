namespace Backend.Models;

/// <summary>
/// The three roles of the system. The role decides what a user is allowed to do
/// (Role-Based Access Control).
/// </summary>
public enum UserRole
{
    Student = 0,
    Faculty = 1,
    Admin = 2
}
