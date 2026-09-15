using System.Security.Cryptography;

namespace Backend.Services;

/// <summary>
/// Turns a plain password into a one-way hash so that the database never
/// stores a readable password.
///
/// Algorithm: PBKDF2 (SHA-256, 100000 iterations) with a random 16-byte salt.
/// Stored format:  {iterations}.{salt-base64}.{hash-base64}
/// </summary>
public class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    /// <summary>Re-hashes the entered password with the stored salt and compares the results.</summary>
    public bool Verify(string password, string storedHash)
    {
        string[] parts = storedHash.Split('.');
        if (parts.Length != 3) return false;

        int iterations = int.Parse(parts[0]);
        byte[] salt = Convert.FromBase64String(parts[1]);
        byte[] expected = Convert.FromBase64String(parts[2]);

        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        // Constant-time comparison so the check cannot be timed by an attacker.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
