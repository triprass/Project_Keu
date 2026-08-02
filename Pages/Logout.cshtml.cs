using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Project_Keu.Pages;

public class LogoutModel : PageModel
{
    /// <summary>
    /// Keluar hanya lewat POST. Kalau lewat GET, sebuah &lt;img&gt; di situs lain
    /// bisa memaksa pengguna keluar tanpa disadari.
    /// </summary>
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Login");
    }

    public IActionResult OnGet() => RedirectToPage("/Login");
}
