using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Project_Keu.Infrastructure;

/// <summary>
/// Melindungi endpoint API yang bersifat mutasi data (POST/PUT/DELETE) dan endpoint
/// yang mengembalikan data pegawai secara massal. Kunci dibaca dari konfigurasi
/// <c>Security:AdminApiKey</c> (set lewat environment variable
/// <c>Security__AdminApiKey</c>, jangan ditulis di appsettings).
///
/// Perilaku:
/// - Development tanpa kunci  -> dilewatkan, supaya pengembangan lokal tidak terhambat.
/// - Production tanpa kunci   -> ditolak 503 (fail closed), bukan dibiarkan terbuka.
/// - Kunci ada                -> wajib header <c>X-Api-Key</c> yang cocok.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireApiKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";

    private const string ConfigurationKey = "Security:AdminApiKey";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        var configuration = services.GetRequiredService<IConfiguration>();
        var environment = services.GetRequiredService<IHostEnvironment>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Project_Keu.Security");

        var configuredKey = configuration[ConfigurationKey];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            if (environment.IsDevelopment())
            {
                return;
            }

            logger.LogError(
                "{ConfigurationKey} belum diset. Endpoint {Path} ditolak agar tidak terbuka untuk publik.",
                ConfigurationKey,
                context.HttpContext.Request.Path);

            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Endpoint belum dikonfigurasi",
                Detail = "Kunci API administratif belum diset pada server."
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
            return;
        }

        var providedKey = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (!MatchesConstantTime(providedKey, configuredKey))
        {
            logger.LogWarning(
                "Percobaan akses tanpa kunci API yang sah ke {Method} {Path}.",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path);

            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Tidak terotorisasi",
                Detail = $"Header {HeaderName} tidak ada atau tidak valid."
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }
    }

    /// <summary>
    /// Bandingkan lewat hash dengan waktu tetap supaya panjang dan isi kunci
    /// tidak bisa disimpulkan dari lama waktu respons.
    /// </summary>
    private static bool MatchesConstantTime(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }

        Span<byte> providedHash = stackalloc byte[32];
        Span<byte> expectedHash = stackalloc byte[32];

        SHA256.HashData(Encoding.UTF8.GetBytes(provided), providedHash);
        SHA256.HashData(Encoding.UTF8.GetBytes(expected), expectedHash);

        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }
}
