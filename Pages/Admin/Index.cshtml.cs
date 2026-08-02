using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Project_Keu.Infrastructure;
using Project_Keu.Services.Admin;

namespace Project_Keu.Pages.Admin;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AdminDashboardService _dashboard;
    private readonly AppTimeZone _timeZone;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(AdminDashboardService dashboard, AppTimeZone timeZone, ILogger<IndexModel> logger)
    {
        _dashboard = dashboard;
        _timeZone = timeZone;
        _logger = logger;
    }

    public AdminDashboardService.Snapshot? Data { get; private set; }

    /// <summary>Diisi bila ringkasan gagal dimuat, supaya beranda tetap tampil dengan menu utuh.</summary>
    public string? LoadError { get; private set; }

    public string Today { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var nowLocal = _timeZone.ToLocal(DateTime.UtcNow);
        Today = nowLocal.ToString("dddd, dd MMMM yyyy", new System.Globalization.CultureInfo("id-ID"));

        try
        {
            // Batas 7 hari dihitung dari awal hari lokal, bukan dari jam sekarang,
            // supaya angkanya cocok dengan cara pengguna membaca kalender.
            var since = _timeZone.StartOfLocalDayUtc(nowLocal.Date.AddDays(-6));
            Data = await _dashboard.GetAsync(since, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Beranda tetap tampil dengan menu utuh, tetapi penyebabnya harus tercatat:
            // galat yang ditelan diam-diam membuat masalahnya mustahil dilacak.
            _logger.LogError(ex, "Gagal memuat ringkasan beranda administrasi.");
            LoadError = "Ringkasan data belum bisa ditampilkan. Periksa catatan aplikasi untuk penyebabnya.";
        }
    }

    public string FormatDate(DateTime utcValue) =>
        _timeZone.ToLocal(utcValue).ToString("dd MMM yyyy HH:mm");

    /// <summary>Persentase batang pada rekap status; nol dijaga agar tidak membagi dengan nol.</summary>
    public static int Percentage(int value, int total) =>
        total <= 0 ? 0 : (int)Math.Round(value * 100d / total);
}
