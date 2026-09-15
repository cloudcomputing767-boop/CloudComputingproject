using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// Uploading and serving resource pictures.
///
/// Upload  : POST /api/images   (admin only, the file is sent as form-data)
/// Display : GET  /api/images/5 (open to everyone, so &lt;img&gt; tags work)
///
/// The upload answers with the address of the new picture, and that address is
/// what gets saved in Resource.ImageUrl.
/// </summary>
[ApiController]
[Route("api/images")]
public class ImagesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ImagesController(AppDbContext db) => _db = db;

    /// <summary>Largest picture we accept: 2 MB.</summary>
    private const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Only ordinary photo formats are accepted.
    /// SVG is deliberately NOT allowed: an SVG file can contain scripts, and we
    /// do not want to store and serve somebody else's script from our own API.
    /// </summary>
    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"]  = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/gif"]  = ".gif"
    };

    /// <summary>Admin only: upload a picture from the computer.</summary>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [RequestSizeLimit(MaxBytes + 4096)]   // a little extra for the form itself
    public async Task<ActionResult> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Please choose an image file to upload." });

        if (file.Length > MaxBytes)
            return BadRequest(new { message = "The image is too large. Please choose a file smaller than 2 MB." });

        if (!AllowedTypes.ContainsKey(file.ContentType))
            return BadRequest(new { message = "Only PNG, JPG, WEBP and GIF images can be uploaded." });

        // Read the file into memory, then store the bytes in PostgreSQL.
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory);
        var bytes = memory.ToArray();

        // Check the real file signature, not just the name the browser sent.
        if (!LooksLikeImage(bytes))
            return BadRequest(new { message = "That file does not look like a valid image." });

        var image = new UploadedImage
        {
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType.ToLowerInvariant(),
            Data = bytes,
            SizeBytes = bytes.Length
        };

        _db.UploadedImages.Add(image);
        await _db.SaveChangesAsync();

        // This address is what the admin saves on the resource.
        return Ok(new
        {
            id = image.Id,
            url = $"/api/images/{image.Id}",
            fileName = image.FileName,
            sizeBytes = image.SizeBytes
        });
    }

    /// <summary>
    /// Sends the picture back to the browser.
    ///
    /// This one is open to everyone on purpose: a browser loading
    /// &lt;img src="/api/images/5"&gt; cannot attach the login token, and a picture
    /// of a classroom is not secret information.
    /// </summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult> Get(int id)
    {
        var image = await _db.UploadedImages.FindAsync(id);
        if (image is null)
            return NotFound();

        // Let the browser keep the picture for a day instead of asking every time.
        Response.Headers.CacheControl = "public, max-age=86400";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        // The answer differs depending on whether the request came from another
        // origin, because a cross-origin request also receives CORS headers.
        // Telling caches that keeps the two apart - otherwise the browser could
        // reuse the cached answer of a plain <img> request for a later fetch()
        // and then refuse it for having no CORS headers.
        // Cross-origin requests already get this header from the CORS
        // middleware, so we only add it for the plain ones.
        if (!Request.Headers.ContainsKey("Origin"))
            Response.Headers.Vary = "Origin";

        return File(image.Data, image.ContentType);
    }

    /// <summary>
    /// Reads the first few bytes ("magic numbers") to confirm the file really is
    /// a picture. A file can always be renamed, so the extension proves nothing.
    /// </summary>
    private static bool LooksLikeImage(byte[] bytes)
    {
        if (bytes.Length < 12) return false;

        // PNG  : 89 50 4E 47
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return true;

        // JPEG : FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return true;

        // GIF  : "GIF8"
        if (bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == '8') return true;

        // WEBP : "RIFF" .... "WEBP"
        if (bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F' &&
            bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P') return true;

        return false;
    }
}
