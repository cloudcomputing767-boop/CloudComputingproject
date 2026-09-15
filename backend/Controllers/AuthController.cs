using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

/// <summary>
/// Registration and login.
///
/// Authentication = proving WHO the user is (email + password).
/// The result is a JWT token that the browser stores and sends back on
/// every following request.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher _hasher;
    private readonly TokenService _tokens;

    public AuthController(AppDbContext db, PasswordHasher hasher, TokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    /// <summary>Creates a new Student or Faculty account.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        // Security: the public page may only create Student or Faculty accounts.
        // An Admin account can only be created by seeding the database.
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role)
            || role == UserRole.Admin)
        {
            return BadRequest(new { message = "Role must be either Student or Faculty." });
        }

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "An account with this email already exists." });

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            Role = role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Ok(BuildResponse(user));
    }

    /// <summary>Checks the password and returns a JWT token.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // The same message is used for "no such user" and "wrong password"
        // so that nobody can discover which emails are registered.
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        return Ok(BuildResponse(user));
    }

    private AuthResponse BuildResponse(User user) => new()
    {
        Token = _tokens.CreateToken(user),
        UserId = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role.ToString()
    };
}
