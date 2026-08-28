using Azure.Core;
using DocumentFormat.OpenXml.Office2016.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;
using Project_Keu.Services.Notifications;

namespace Project_Keu.Pages;

public class FormPertanyaanModel : PageModel
{
    /// <summary>Batas panjang isi pertanyaan agar satu request tidak bisa mengirim payload raksasa.</summary>
    private const int MaxQuestionLength = 4000;

    private const int MaxQuestionNoAttempts = 3;

    private static readonly Guid DefaultStatusId = Guid.Parse("589362d4-83e4-457f-af89-dad137b68845");

    private readonly AppDbContext _context;
    private readonly NotificationQueue _notifications;
    private readonly ILogger<FormPertanyaanModel> _logger;

    private readonly IFonnteService _fonnteService; // Inject FonnteService

    public FormPertanyaanModel(
        AppDbContext context,
        NotificationQueue notifications,
        ILogger<FormPertanyaanModel> logger,
        IFonnteService fonnteService)
    {
        _context = context;
        _notifications = notifications;
        _logger = logger;
        _fonnteService = fonnteService;
    }

    public List<QuestionCategory> Categories { get; private set; } = new();

    [BindProperty]
    public string? Pertanyaan { get; set; }

    [BindProperty]
    public Guid? CategoryId { get; set; }

    [BindProperty]
    public string? Nip { get; set; }

    [BindProperty]
    public string? Nama { get; set; }

    [BindProperty]
    public Guid? EmployeeId { get; set; }

    public Task OnGetAsync(CancellationToken cancellationToken)
    {
        return LoadCategoriesAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await LoadCategoriesAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(Pertanyaan) || CategoryId is null || EmployeeId is null)
        {
            ModelState.AddModelError(string.Empty, "Data belum lengkap. Mohon isi pertanyaan, kategori, dan NIP yang valid.");
            return Page();
        }

        var questionText = Pertanyaan.Trim();

        if (questionText.Length > MaxQuestionLength)
        {
            ModelState.AddModelError(string.Empty, $"Pertanyaan terlalu panjang. Maksimal {MaxQuestionLength} karakter.");
            return Page();
        }

        var category = await _context.QuestionCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == CategoryId.Value && c.IsActive, cancellationToken);

        if (category is null)
        {
            ModelState.AddModelError(string.Empty, "Kategori tidak valid.");
            return Page();
        }

        var employeeExists = await _context.Employees
            .AsNoTracking()
            .AnyAsync(e => e.Id == EmployeeId.Value, cancellationToken);

        if (!employeeExists)
        {
            ModelState.AddModelError(string.Empty, "Pegawai tidak valid.");
            return Page();
        }

        var defaultStatusExists = await _context.QuestionStatuses
            .AsNoTracking()
            .AnyAsync(s => s.Id == DefaultStatusId, cancellationToken);

        if (!defaultStatusExists)
        {
            _logger.LogError("Status default {StatusId} tidak ada di tb_m_question_status.", DefaultStatusId);
            ModelState.AddModelError(string.Empty, "Konfigurasi status pertanyaan belum lengkap. Hubungi administrator.");
            return Page();
        }

        var question = new Question
        {
            Id = Guid.NewGuid(),
            CategoryId = CategoryId.Value,
            Title = category.Name,
            QuestionText = questionText,
            CreatedByEmployee = EmployeeId.Value,
            StatusId = DefaultStatusId,
            CreatedAt = DateTime.UtcNow
        };

        if (!await SaveWithGeneratedQuestionNoAsync(question, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "Pertanyaan gagal disimpan. Silakan coba lagi.");
            return Page();
        }

        // Dititipkan ke antrean, bukan dikirim di sini: pegawai tidak boleh menunggu
        // WAHA, dan pemberitahuan yang gagal tidak boleh membatalkan pertanyaan yang
        // sudah tersimpan.
        _notifications.Enqueue(NotificationJob.QuestionCreated(question.Id));

        return RedirectToPage("/Pertanyaan");
    }

    /// <summary>
    /// Nomor pertanyaan dihitung dari baris terakhir, sehingga dua pengiriman yang
    /// hampir bersamaan bisa menghasilkan nomor yang sama. Penyimpanan diulang
    /// beberapa kali bila database menolak karena bentrok.
    /// </summary>
    private async Task<bool> SaveWithGeneratedQuestionNoAsync(Question question, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxQuestionNoAttempts; attempt++)
        {
            question.QuestionNo = await GenerateQuestionNoAsync(cancellationToken);
            _context.Questions.Add(question);

            // Ambil Data Pegawai
            var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == question.CreatedByEmployee);
            string ticketNo = question.QuestionNo;
            string senderName = employee.FullName;
            string nip = employee.Nip;
            string unitKerja = employee.Branch;
            string kategoriPertanyaan = question.Title;
            string targetPhone = employee.PhoneNumber;

            // Kirim Pesan Notifikasi ke Pembuat Pertanyaan [Open] Parameter = ticketNo, senderName, nip, unitKerja, kategoriPertanyaan
            string messageBody = _fonnteService.BuildTicketTemplate1(ticketNo, senderName, nip, unitKerja, kategoriPertanyaan);
            await _fonnteService.SendWhatsAppMessageAsync(targetPhone, messageBody);

            // Kirim Pesan Notifikasi ke PIC Keuangan [Open] Parameter = ticketNo, senderName, nip, unitKerja, kategoriPertanyaan

            // 082111191354     Pak Mario
            // 083145710015     Aji
            // 081337645975     Dinu
            string targetPhonePIC = "082111191354,083145710015,081337645975";

            string messageBody2 = _fonnteService.BuildTicketTemplate2(ticketNo, senderName, nip, unitKerja, kategoriPertanyaan);
            await _fonnteService.SendWhatsAppMessageAsync(targetPhonePIC, messageBody2);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);

                return true;
            }
            catch (DbUpdateException ex)
            {
                _context.Entry(question).State = EntityState.Detached;

                if (attempt == MaxQuestionNoAttempts)
                {
                    _logger.LogError(ex, "Gagal menyimpan pertanyaan setelah {Attempts} percobaan.", attempt);
                    return false;
                }

                _logger.LogWarning(ex, "Percobaan {Attempt} menyimpan pertanyaan gagal, mencoba nomor berikutnya.", attempt);
            }
        }

        return false;
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        Categories = await _context.QuestionCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    private async Task<string> GenerateQuestionNoAsync(CancellationToken cancellationToken)
    {
        var prefix = $"Q{DateTime.UtcNow:yyyyMM}";

        // Nomor terakhir diambil langsung lewat ORDER BY ... LIMIT 1 di database,
        // bukan dengan menarik seluruh baris bulan berjalan ke memori aplikasi.
        var lastNo = await _context.Questions
            .AsNoTracking()
            .Where(q => q.QuestionNo != null && q.QuestionNo.StartsWith(prefix))
            .OrderByDescending(q => q.QuestionNo!.Length)
            .ThenByDescending(q => q.QuestionNo)
            .Select(q => q.QuestionNo!)
            .FirstOrDefaultAsync(cancellationToken);

        var lastRunningNumber = 0;

        if (lastNo is not null && lastNo.Length > prefix.Length &&
            int.TryParse(lastNo.AsSpan(prefix.Length), out var parsed))
        {
            lastRunningNumber = parsed;
        }

        return $"{prefix}{lastRunningNumber + 1:D3}";
    }
}
