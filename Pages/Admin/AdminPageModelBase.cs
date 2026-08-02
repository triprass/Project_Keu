using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Project_Keu.Pages.Admin;

/// <summary>
/// Perkakas bersama seluruh halaman panel administrasi: pencarian, penomoran
/// halaman, dan pesan hasil tindakan.
///
/// Yang muncul di URL hanya keadaan daftar (kata kunci, nomor halaman, saringan)
/// supaya halaman bisa ditandai dan tombol "kembali" peramban tetap benar.
/// Identitas baris tidak pernah ikut ke URL: seluruh tindakan ubah dan hapus
/// mengirim id di dalam badan permintaan POST.
/// </summary>
public abstract class AdminPageModelBase : PageModel
{
    public const int PageSize = 15;

    /// <summary>Batas panjang kata kunci, sekaligus membatasi biaya kueri LIKE.</summary>
    protected const int MaxSearchLength = 100;

    /// <summary>Karakter pelolos untuk pola LIKE; harus sama dengan yang diberikan ke ILike.</summary>
    protected const string LikeEscape = "\\";

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    /// <summary>Jumlah baris yang lolos saringan, sebelum dipotong per halaman.</summary>
    public int TotalItems { get; protected set; }

    /// <summary>Jumlah seluruh baris tanpa saringan; dipakai membedakan "kosong" dari "tidak ketemu".</summary>
    public int TotalUnfiltered { get; protected set; }

    public int TotalPages => TotalItems == 0 ? 1 : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasSearch => !string.IsNullOrWhiteSpace(Search);

    /// <summary>Nomor urut baris pertama di halaman ini, untuk kolom "No".</summary>
    public int RowOffset => (PageNumber - 1) * PageSize;

    /// <summary>Dialog dibuka kembali saat validasi gagal, lengkap dengan isian pengguna.</summary>
    public bool ReopenDialog { get; protected set; }

    /// <summary>Kata kunci yang sudah dipangkas dan dibatasi panjangnya, atau null bila kosong.</summary>
    protected string? NormalizedSearch()
    {
        var value = Search?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.Length > MaxSearchLength)
        {
            value = value[..MaxSearchLength];
        }

        Search = value;
        return value;
    }

    /// <summary>
    /// Pola "%kata%" dengan karakter khusus LIKE dilolosakan, supaya pengguna yang
    /// mengetik % atau _ tidak berubah menjadi pencarian seluruh tabel.
    /// </summary>
    protected static string ContainsPattern(string term)
    {
        var escaped = term
            .Replace(LikeEscape, LikeEscape + LikeEscape)
            .Replace("%", LikeEscape + "%")
            .Replace("_", LikeEscape + "_");

        return $"%{escaped}%";
    }

    /// <summary>
    /// Menjepit nomor halaman ke rentang yang masih ada isinya. Dipanggil setelah
    /// jumlah baris diketahui, agar saringan yang menyusutkan hasil tidak membuat
    /// pengguna terdampar di halaman kosong.
    /// </summary>
    protected int ClampPage()
    {
        if (PageNumber < 1)
        {
            PageNumber = 1;
        }

        var lastPage = TotalPages;

        if (PageNumber > lastPage)
        {
            PageNumber = lastPage;
        }

        return PageNumber;
    }

    protected void Notify(string message) => TempData["AdminSuccess"] = message;

    protected void NotifyError(string message) => TempData["AdminError"] = message;

    /// <summary>
    /// Keadaan daftar yang harus ikut terbawa pada tautan halaman dan pengalihan
    /// setelah menyimpan. Halaman dengan saringan tambahan menambahkannya di sini,
    /// sehingga saringan tidak hilang saat berpindah halaman.
    /// </summary>
    public virtual Dictionary<string, object?> ListState() => new() { ["q"] = Search };

    /// <summary>Nilai rute untuk satu nomor halaman tertentu, dipakai oleh penomoran halaman.</summary>
    public Dictionary<string, object?> ListStateForPage(int page)
    {
        var values = ListState();
        values["p"] = page;
        return values;
    }

    /// <summary>Kembali ke daftar dengan keadaan saringan yang sama (pola POST-redirect-GET).</summary>
    protected IActionResult RedirectToList() => RedirectToPage(ListStateForPage(PageNumber));

    /// <summary>Nama pengguna yang sedang masuk, untuk kolom created_by / updated_by.</summary>
    protected string CurrentUserName =>
        User.FindFirst("username")?.Value ?? User.Identity?.Name ?? "system";
}
