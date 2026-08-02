using System.Security.Cryptography;
using System.Text;

namespace Project_Keu.Infrastructure;

/// <summary>
/// Hash kata sandi PBKDF2-HMAC-SHA512. Kata sandi asli tidak pernah disimpan;
/// yang tersimpan hanya "PBKDF2-SHA512$iterasi$salt$hash" dengan salt acak per akun.
/// </summary>
public static class PasswordHasher
{
    private const string Prefix = "PBKDF2-SHA512";

    // OWASP menganjurkan minimal 210.000 iterasi untuk PBKDF2-HMAC-SHA512.
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, KeySize);

        return string.Join('$', Prefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    /// <summary>
    /// Memeriksa kata sandi terhadap hash tersimpan. Mengembalikan false untuk hash
    /// yang rusak formatnya, bukan melempar, supaya satu baris data yang cacat tidak
    /// menjatuhkan halaman login.
    /// </summary>
    public static bool Verify(string? password, string? storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        var parts = storedHash.Split('$');

        if (parts.Length != 4 ||
            !string.Equals(parts[0], Prefix, StringComparison.Ordinal) ||
            !int.TryParse(parts[1], out var iterations) ||
            iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedKey;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedKey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (expectedKey.Length == 0)
        {
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, Algorithm, expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }

    /// <summary>
    /// Perhitungan tiruan dengan biaya setara, dijalankan saat nama pengguna tidak
    /// ditemukan supaya lama waktu respons tidak membocorkan akun mana yang ada.
    /// </summary>
    public static void PerformDummyWork()
    {
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes("dummy"), RandomNumberGenerator.GetBytes(SaltSize), Iterations, Algorithm, KeySize);
    }
}
