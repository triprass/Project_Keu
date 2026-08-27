using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure;
using Project_Keu.Models;

namespace Project_Keu.Pages;

public class DetailPertanyaanModel : PageModel
{
    // Container dengan globalization invariant tidak mengenal "id-ID"; kalau tidak
    // tersedia, format tanggal jatuh ke culture invariant daripada halaman gagal.
    private static readonly CultureInfo DisplayCulture = CreateDisplayCulture();

    private readonly AppDbContext _context;
    private readonly AppTimeZone _appTimeZone;

    public DetailPertanyaanModel(AppDbContext context, AppTimeZone appTimeZone)
    {
        _context = context;
        _appTimeZone = appTimeZone;
    }

    // Dikirim lewat form body (POST) supaya id pertanyaan tidak tampil di URL.
    //[BindProperty]
    public Guid? Id { get; set; }

    public Question? SelectedQuestion { get; private set; }

    public string AnswerTextDisplay { get; private set; } = "Belum ada respon dari PIC Keuangan.";

    public bool IsClosed { get; private set; }

    public string QuestionNoDisplay { get; private set; } = string.Empty;
    public string CategoryNameDisplay { get; private set; } = string.Empty;
    public string EmployeeNameDisplay { get; private set; } = string.Empty;
    public string CreatedAtDisplay { get; private set; } = string.Empty;
    public string AnsweredByDisplay { get; private set; } = string.Empty;
    public string AnsweredAtDisplay { get; private set; } = string.Empty;

    /// <summary>Kategori pertanyaan, dipakai tombol "Kembali" agar mendarat di daftar yang sama.</summary>
    public Guid? CategoryId => SelectedQuestion?.CategoryId;

    public IActionResult OnGet()
    {
        // Halaman ini hanya bisa dibuka lewat POST dari daftar pertanyaan.
        // Akses langsung tanpa id ditampilkan sebagai halaman kosong.
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!Id.HasValue || Id.Value == Guid.Empty)
        {
            return Page();
        }

        SelectedQuestion = await _context.Questions
            .AsNoTracking()
            .Include(q => q.Category)
            .Include(q => q.CreatedByEmployeeNavigation)
            .FirstOrDefaultAsync(q => q.Id == Id.Value, cancellationToken);

        if (SelectedQuestion is null)
        {
            return Page();
        }

        QuestionNoDisplay = string.IsNullOrWhiteSpace(SelectedQuestion.QuestionNo) ? "-" : SelectedQuestion.QuestionNo;
        CategoryNameDisplay = SelectedQuestion.Category?.Name ?? "-";
        EmployeeNameDisplay = SelectedQuestion.CreatedByEmployeeNavigation?.FullName ?? "-";
        CreatedAtDisplay = FormatDate(SelectedQuestion.CreatedAt);

        var latestAnswer = await _context.Answers
            .AsNoTracking()
            .Include(a => a.AnsweredByEmployee)
            .Where(a => a.QuestionId == SelectedQuestion.Id)
            // Jawaban tanpa tanggal diletakkan paling belakang. Versi sebelumnya
            // memakai "?? DateTime.MinValue" yang dikirim Npgsql sebagai DateTime
            // ber-Kind Unspecified dan ditolak kolom timestamptz.
            .OrderByDescending(a => a.AnsweredAt.HasValue)
            .ThenByDescending(a => a.AnsweredAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestAnswer is not null && !string.IsNullOrWhiteSpace(latestAnswer.AnswerText))
        {
            AnswerTextDisplay = latestAnswer.AnswerText;
            IsClosed = true;
            AnsweredByDisplay = latestAnswer.AnsweredByEmployee?.FullName ?? "-";
            AnsweredAtDisplay = latestAnswer.AnsweredAt.HasValue
                ? FormatDate(latestAnswer.AnsweredAt.Value)
                : string.Empty;
        }

        return Page();
    }

    /// <summary>Tanggal ditampilkan pada zona waktu aplikasi, sama seperti pada tabel daftar pertanyaan.</summary>
    private string FormatDate(DateTime utcValue)
    {
        return _appTimeZone.ToLocal(utcValue).ToString("dd MMMM yyyy, HH:mm", DisplayCulture);
    }

    private static CultureInfo CreateDisplayCulture()
    {
        try
        {
            return new CultureInfo("id-ID");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
