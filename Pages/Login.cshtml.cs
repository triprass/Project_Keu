using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Project_Keu.Infrastructure;

namespace Project_Keu.Pages;

[EnableRateLimiting("login")]
public class LoginModel : PageModel
{
    /// <summary>Pesan yang sama untuk nama pengguna salah maupun sandi salah, agar tidak membocorkan mana yang keliru.</summary>
    private const string InvalidCredentialMessage = "Nama pengguna atau kata sandi salah.";

    private readonly AdminCredentialService _credentials;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(AdminCredentialService credentials, ILogger<LoginModel> logger)
    {
        _credentials = credentials;
        _logger = logger;
    }

    [BindProperty]
    [Required(ErrorMessage = "Nama pengguna wajib diisi.")]
    [StringLength(100, ErrorMessage = "Nama pengguna terlalu panjang.")]
    public string? Username { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Kata sandi wajib diisi.")]
    [StringLength(200, ErrorMessage = "Kata sandi terlalu panjang.")]
    public string? Password { get; set; }

    [BindProperty]
    public bool RememberMe { get; set; }

    public string? ErrorMessage { get; private set; }

    /// <summary>Kredensial administrator belum diisi di server, jadi login belum bisa dipakai.</summary>
    public bool IsSetupRequired => !_credentials.IsConfigured;

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/PageQuestion");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (IsSetupRequired)
        {
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!_credentials.Verify(Username, Password))
        {
            _logger.LogWarning(
                "Login administrator gagal untuk '{Username}' dari {RemoteIp}.",
                Username,
                HttpContext.Connection.RemoteIpAddress);

            ErrorMessage = InvalidCredentialMessage;
            Password = null;
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, _credentials.DisplayName),
            new(ClaimTypes.Role, "Administrator")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = RememberMe,
                ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : null
            });

        _logger.LogInformation("Login administrator berhasil dari {RemoteIp}.", HttpContext.Connection.RemoteIpAddress);

        // Hanya URL lokal yang diterima, supaya returnUrl tidak bisa dipakai
        // mengarahkan pengguna ke situs luar setelah login.
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/PageQuestion");
    }
}
