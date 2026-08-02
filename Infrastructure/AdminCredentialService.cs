using System.Security.Cryptography;
using System.Text;

namespace Project_Keu.Infrastructure;

/// <summary>
/// Kredensial administrator dibaca dari konfigurasi, bukan dari database, karena
/// belum ada tabel pengguna pada skema saat ini. Kata sandi tidak pernah disimpan
/// apa adanya - yang disimpan hanya hash PBKDF2 berikut salt acaknya.
///
/// Set lewat environment variable:
///   Admin__Username     = nama pengguna
///   Admin__PasswordHash = keluaran dari "dotnet Project_Keu.dll --hash-password &lt;kata-sandi&gt;"
/// </summary>
public sealed class AdminCredentialService
{
    private const string Prefix = "PBKDF2-SHA512";

    // OWASP menganjurkan minimal 210.000 iterasi untuk PBKDF2-HMAC-SHA512.
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    private readonly string? _username;
    private readonly string? _passwordHash;
    private readonly ILogger<AdminCredentialService> _logger;

    public AdminCredentialService(IConfiguration configuration, ILogger<AdminCredentialService> logger)
    {
        _logger = logger;
        _username = configuration["Admin:Username"];
        _passwordHash = configuration["Admin:PasswordHash"];
    }

    /// <summary>Kredensial administrator sudah diisi pada konfigurasi server.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_passwordHash);

    public string DisplayName => string.IsNullOrWhiteSpace(_username) ? "Administrator" : _username;

    /// <summary>
    /// Memeriksa kredensial. Perhitungan hash tetap dijalankan meskipun nama pengguna
    /// salah, supaya lama waktu respons tidak membocorkan nama pengguna mana yang benar.
    /// </summary>
    public bool Verify(string? username, string? password)
    {
        if (!IsConfigured)
        {
            return false;
        }

        var usernameMatches = FixedTimeTextEquals(username, _username!);
        var passwordMatches = VerifyPassword(password ?? string.Empty, _passwordHash!);

        return usernameMatches && passwordMatches;
    }

    /// <summary>Membuat hash baru untuk disimpan pada konfigurasi.</summary>
    public static string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, KeySize);

        return string.Join('$', Prefix, Iterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
    }

    private bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split('$');

        if (parts.Length != 4 ||
            !string.Equals(parts[0], Prefix, StringComparison.Ordinal) ||
            !int.TryParse(parts[1], out var iterations) ||
            iterations <= 0)
        {
            _logger.LogError("Admin:PasswordHash tidak berformat '{Prefix}$iterasi$salt$hash'.", Prefix);
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
            _logger.LogError("Salt atau hash pada Admin:PasswordHash bukan Base64 yang sah.");
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, Algorithm, expectedKey.Length);

        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }

    /// <summary>Perbandingan teks dengan waktu tetap, lewat hash agar panjang tidak terbaca dari waktu eksekusi.</summary>
    private static bool FixedTimeTextEquals(string? left, string right)
    {
        Span<byte> leftHash = stackalloc byte[64];
        Span<byte> rightHash = stackalloc byte[64];

        SHA512.HashData(Encoding.UTF8.GetBytes(left ?? string.Empty), leftHash);
        SHA512.HashData(Encoding.UTF8.GetBytes(right), rightHash);

        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }
}
