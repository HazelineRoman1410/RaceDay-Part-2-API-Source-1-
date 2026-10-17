using System.Security.Cryptography;

namespace RaceDay.Api.Services;

/// <summary>
/// Salted PBKDF2-SHA256 hashing. Stored format: PBKDF2$iterations$saltBase64$keyBase64.
/// A unique random salt per password means two users with the same password get different hashes,
/// and the iteration count is stored so it can be raised later without breaking old hashes.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Prefix = "PBKDF2";
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);
        return string.Join('$', Prefix, Iterations.ToString(), Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    public bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash)) return false;

        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations) || iterations <= 0)
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expected.Length);
        // Constant-time comparison so response timing does not leak how many bytes matched.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
