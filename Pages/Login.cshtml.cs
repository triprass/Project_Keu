using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Project_Keu.Infrastructure.Authorization;
using Project_Keu.Services.Admin;

namespace Project_Keu.Pages;

[EnableRateLimiting("login")]
public class LoginModel : PageModel
{
    /// <summary>Pesan yang sama untuk nama pengguna salah maupun sandi salah, agar tidak membocorkan mana yang keliru.</summary>
    private const string InvalidCredentialMessage = "Nama pengguna atau kata sandi salah.";

    private readonly AdminAccountService _accounts;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(AdminAccountService accounts, ILogger<LoginModel> logger)
    {
        _accounts = accounts;
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

    /// <summary>Belum ada akun admin aktif di database, jadi login belum bisa dipakai.</summary>
    public bool IsSetupRequired { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Admin/Index");
        }

        IsSetupRequired = !await HasAccountsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl, CancellationToken cancellationToken)
    {
        IsSetupRequired = !await HasAccountsAsync(cancellationToken);

        if (IsSetupRequired || !ModelState.IsValid)
        {
            return Page();
        }

        var result = await _accounts.AuthenticateAsync(Username, Password, cancellationToken);

        if (!result.Succeeded)
        {
            Password = null;

            ErrorMessage = result.Outcome switch
            {
                AdminAccountService.AuthOutcome.LockedOut =>
                    "Akun terkunci sementara karena terlalu banyak percobaan gagal. Coba lagi beberapa saat lagi.",
                AdminAccountService.AuthOutcome.Disabled =>
                    "Akun ini dinonaktifkan. Hubungi pengelola sistem.",
                _ => InvalidCredentialMessage
            };

            _logger.LogWarning(
                "Login administrator gagal ({Outcome}) untuk '{Username}' dari {RemoteIp}.",
                result.Outcome, Username, HttpContext.Connection.RemoteIpAddress);

            return Page();
        }

        await SignInAsync(result);

        _logger.LogInformation(
            "Login administrator berhasil untuk '{Username}' dari {RemoteIp}.",
            result.Username, HttpContext.Connection.RemoteIpAddress);

        // Hanya URL lokal yang diterima, supaya returnUrl tidak bisa dipakai
        // mengarahkan pengguna ke situs luar setelah login.
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToPage("/Admin/Index");
    }

    private async Task SignInAsync(AdminAccountService.AuthResult result)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(ClaimTypes.Name, result.FullName),
            new("username", result.Username)
        };

        claims.AddRange(result.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(result.Permissions.Select(permission => new Claim(AppClaimTypes.Permission, permission)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = RememberMe,
                ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : null
            });
    }

    /// <summary>
    /// Database bisa saja belum bisa dihubungi. Kalau begitu halaman tetap tampil
    /// dengan pesan penyiapan, bukan melempar galat ke pengguna.
    /// </summary>
    private async Task<bool> HasAccountsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _accounts.HasAnyActiveAccountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal memeriksa akun administrator di database.");
            return false;
        }
    }
}
