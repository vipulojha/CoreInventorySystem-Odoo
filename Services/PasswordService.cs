using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace CoreInventory.Services;

public sealed class PasswordService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public bool MeetsPolicy(string password)
    {
        return password.Length >= 8
               && Regex.IsMatch(password, "[a-z]")
               && Regex.IsMatch(password, "[A-Z]")
               && Regex.IsMatch(password, "[^a-zA-Z0-9]");
    }

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var deriveBytes = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA1);
        var hash = deriveBytes.GetBytes(HashSize);

        return $"PBKDF2-SHA1${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return false;
        }

        var parts = passwordHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !string.Equals(parts[0], "PBKDF2-SHA1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        using var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA1);
        var actual = deriveBytes.GetBytes(expected.Length);

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
