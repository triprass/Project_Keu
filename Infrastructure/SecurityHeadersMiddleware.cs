namespace Project_Keu.Infrastructure;

/// <summary>
/// Menambahkan header keamanan dasar pada setiap respons HTML/JSON.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), interest-cohort=()";

        // Halaman memakai Google Fonts dan beberapa blok <script> inline,
        // jadi 'unsafe-inline' masih diperlukan untuk script & style.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "img-src 'self' data:; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com; " +
            "script-src 'self' 'unsafe-inline'; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'";

        // Header bawaan Kestrel yang membocorkan detail server.
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        return _next(context);
    }
}
