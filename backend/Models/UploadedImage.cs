namespace Backend.Models;

/// <summary>
/// A picture the administrator uploaded from their own computer.
///
/// The file itself is kept in the database as a column of type "bytea"
/// (PostgreSQL's binary type). A real company would usually put the file in
/// cloud file storage (Amazon S3, Azure Blob Storage) and keep only the link
/// here - but for this case study, keeping everything in the one cloud
/// database means the whole system is still just two services: Render + Neon.
/// </summary>
public class UploadedImage
{
    public int Id { get; set; }

    /// <summary>Original name of the file, shown to the admin.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>For example "image/png" - the browser needs this to show the picture.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>The raw bytes of the picture.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public int SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
