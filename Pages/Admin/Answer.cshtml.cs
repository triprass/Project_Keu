using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure;
using Project_Keu.Infrastructure.Admin;
using Project_Keu.Models;
using Project_Keu.Services.Notifications;

namespace Project_Keu.Pages.Admin;

/// <summary>
/// Layar menjawab satu pertanyaan, sekaligus mengubah jawaban yang sudah ada.
/// Hanya bisa dibuka lewat POST dari daftar, sehingga id pertanyaan tidak
/// pernah muncul di URL maupun di catatan akses peladen.
///
/// Penjawab tidak dipilih, melainkan diambil dari akun yang sedang masuk: hanya
/// pengelola berakun yang boleh menjawab, jadi identitasnya sudah pasti.
/// </summary>
[Authorize(Policy = AdminMenu.PolicyAnswer)]
public class AnswerModel : PageModel
{
    private static readonly CultureInfo DisplayCulture = CreateDisplayCulture();
    private readonly IFonnteService _fonnteService; // Inject FonnteService
    private readonly IWablasService _wablasService;

    private readonly AppDbContext _context;
    private readonly AppTimeZone _appTimeZone;
    private readonly NotificationQueue _notifications;


    public AnswerModel(AppDbContext context, AppTimeZone appTimeZone, NotificationQueue notifications, IFonnteService fonnteService, IWablasService wablasService)
    {
        _context = context;
        _appTimeZone = appTimeZone;
        _notifications = notifications;
        _fonnteService = fonnteService;
        _wablasService = wablasService;
    }

    /// <summary>Id pertanyaan; selalu dikirim di badan POST.</summary>
    [BindProperty]
    public Guid? Id { get; set; }

    [BindProperty]
    public AnswerInput Input { get; set; } = new();

    public sealed class AnswerInput
    {
        [Required(ErrorMessage = "Jawaban wajib diisi.")]
        [StringLength(8000, ErrorMessage = "Jawaban maksimal 8.000 karakter.")]
        public string AnswerText { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pilih status pertanyaan.")]
        public Guid? StatusId { get; set; }
    }

    public Question? SelectedQuestion { get; private set; }

    /// <summary>Benar bila pertanyaan ini sudah punya jawaban, sehingga layarnya berarti mengubah.</summary>
    public bool IsEditing { get; private set; }

    /// <summary>Kode ikut dibawa karena penebakan status awal membacanya, bukan hanya namanya.</summary>
    public sealed record StatusChoice(Guid Id, string Code, string Name);

    public List<StatusChoice> StatusOptions { get; private set; } = [];

    public string QuestionNoDisplay { get; private set; } = "-";
    public string CategoryNameDisplay { get; private set; } = "-";
    public string EmployeeNameDisplay { get; private set; } = "-";
    public string BranchDisplay { get; private set; } = "-";
    public string CreatedAtDisplay { get; private set; } = string.Empty;
    public string AnsweredByDisplay { get; private set; } = string.Empty;
    public string AnsweredAtDisplay { get; private set; } = string.Empty;

    /// <summary>Pegawai yang tertaut ke akun yang sedang masuk; inilah yang tercatat sebagai penjawab.</summary>
    public Guid? AnsweringEmployeeId { get; private set; }

    public string AnsweringEmployeeName { get; private set; } = string.Empty;

    public string CurrentUserName =>
        User.FindFirstValue("username") ?? User.Identity?.Name ?? "-";

    /// <summary>
    /// Akun ini belum bisa menjawab karena belum tertaut ke data pegawai. Kolom
    /// <c>answered_by</c> menunjuk ke tabel pegawai, jadi tanpa tautan itu jawaban
    /// tidak punya pemilik yang sah.
    /// </summary>
    public bool CannotAnswer => AnsweringEmployeeId is null;

    /// <summary>Dibuka langsung tanpa konteks pertanyaan; kembalikan ke beranda.</summary>
    public IActionResult OnGet() => RedirectToPage("/Admin/Index");

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            TempData["AdminError"] = "Pertanyaan tidak ditemukan.";
            return RedirectToPage("/Admin/Index");
        }

        // POST ini hanya membuka layar, bukan mengirim isian. Tanpa dibersihkan,
        // kolom wajib yang memang masih kosong langsung tampil sebagai galat merah
        // padahal pengguna belum sempat mengetik apa pun.
        ModelState.Clear();

        await FillDefaultsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!await LoadAsync(cancellationToken))
        {
            TempData["AdminError"] = "Pertanyaan tidak ditemukan.";
            return RedirectToPage("/Admin/Index");
        }

        // Diperiksa lagi di sini, bukan hanya disembunyikan di tampilan: formulir
        // yang dikirim peramban tidak pernah menjadi dasar keputusan.
        if (CannotAnswer)
        {
            ModelState.AddModelError(string.Empty,
                "Akun Anda belum tertaut ke data pegawai, sehingga jawaban belum dapat disimpan. " +
                "Hubungkan akun ini dengan seorang pegawai lewat menu Akun Admin.");
        }

        Input.AnswerText = Input.AnswerText?.Trim() ?? string.Empty;

        if (Input.StatusId.HasValue &&
            !await _context.QuestionStatuses.AnyAsync(s => s.Id == Input.StatusId.Value, cancellationToken))
        {
            ModelState.AddModelError("Input.StatusId", "Status yang dipilih tidak ditemukan.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        bool isAnswered = false;
        var now = DateTime.UtcNow;
        var existing = await LatestAnswerQuery(SelectedQuestion!.Id).FirstOrDefaultAsync(cancellationToken);

        if (existing is null)
        {
            _context.Answers.Add(new Models.Answer
            {
                Id = Guid.NewGuid(),
                QuestionId = SelectedQuestion.Id,
                AnswerText = Input.AnswerText,
                AnsweredBy = AnsweringEmployeeId!.Value,
                AnsweredAt = now
            });
        }
        else
        {
            isAnswered = true;
            existing.AnswerText = Input.AnswerText;

            // Yang mengubah jawaban menjadi penanggung jawab isinya yang sekarang.
            existing.AnsweredBy = AnsweringEmployeeId!.Value;

            // Kolom ini satu-satunya penanda waktu pada jawaban, jadi diperbarui
            // agar mencerminkan kapan teks yang sekarang tampil itu ditulis.
            existing.AnsweredAt = now;
        }

        // Status diambil dari pilihan pengguna, bukan ditebak, supaya daftar
        // pertanyaan menunjukkan keadaan yang memang dimaksudkan.
        var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == SelectedQuestion.Id, cancellationToken);

        if (question is not null && question.StatusId != Input.StatusId!.Value)
        {
            question.StatusId = Input.StatusId.Value;
            question.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (!isAnswered)
        {
            

            // Ambil Data Pegawai
            var employee = await _context.Employees.FirstOrDefaultAsync(x => x.Id == question.CreatedByEmployee);
            string ticketNo = question.QuestionNo;
            string senderName = employee.FullName;
            string nip = employee.Nip;
            string unitKerja = employee.Branch;
            string kategoriPertanyaan = question.Title;
            string targetPhone = employee.PhoneNumber;

            // [SCRIPT PUSH NOTIFICATION FONNTE]

            // Kirim Pesan Notifikasi ke Pembuat Pertanyaan [Close] Parameter = ticketNo, senderName, nip, unitKerja, kategoriPertanyaan
            //string messageBody = _fonnteService.BuildTicketTemplate3(ticketNo, senderName, nip, unitKerja, kategoriPertanyaan, SelectedQuestion.Id);
            //await _fonnteService.SendWhatsAppMessageAsync(targetPhone, messageBody);

            // [END OF SCRIPT PUSH NOTIFICATION FONNTE]


            // [SCRIPT PUSH NOTIFICATION WABLAS]

            // Kirim Pesan Notifikasi ke Pembuat Pertanyaan [Close]
            //string messageBody = _wablasService.BuildTicketTemplate3(ticketNo, senderName, nip, unitKerja, kategoriPertanyaan, SelectedQuestion.Id);
            //await _wablasService.SendWhatsAppMessageAsync(targetPhone, messageBody);

            // [END OF SCRIPT PUSH NOTIFICATION WABLAS]
        }

        // Setelah tersimpan, bukan sebelumnya: penanya hanya diberi tahu tentang
        // jawaban yang benar-benar sudah masuk database.
        _notifications.Enqueue(NotificationJob.QuestionAnswered(SelectedQuestion.Id));

        TempData["AdminSuccess"] = existing is null
            ? $"Jawaban untuk {QuestionNoDisplay} berhasil disimpan."
            : $"Jawaban untuk {QuestionNoDisplay} berhasil diperbarui.";

        return RedirectToPage("/Admin/Master/Questions");
    }

    /// <summary>Memuat pertanyaan beserta pilihan status. False bila pertanyaannya tidak ada.</summary>
    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        await ResolveAnsweringEmployeeAsync(cancellationToken);

        if (!Id.HasValue || Id.Value == Guid.Empty)
        {
            return false;
        }

        SelectedQuestion = await _context.Questions
            .AsNoTracking()
            .Include(q => q.Category)
            .Include(q => q.CreatedByEmployeeNavigation)
            .FirstOrDefaultAsync(q => q.Id == Id.Value, cancellationToken);

        if (SelectedQuestion is null)
        {
            return false;
        }

        QuestionNoDisplay = string.IsNullOrWhiteSpace(SelectedQuestion.QuestionNo) ? "-" : SelectedQuestion.QuestionNo;
        CategoryNameDisplay = SelectedQuestion.Category?.Name ?? "-";
        EmployeeNameDisplay = SelectedQuestion.CreatedByEmployeeNavigation?.FullName ?? "-";
        BranchDisplay = SelectedQuestion.CreatedByEmployeeNavigation?.Branch ?? "-";
        CreatedAtDisplay = FormatDate(SelectedQuestion.CreatedAt);

        StatusOptions = await _context.QuestionStatuses
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new StatusChoice(s.Id, s.Code, s.Name))
            .ToListAsync(cancellationToken);

        var latest = await LatestAnswerQuery(SelectedQuestion.Id)
            .AsNoTracking()
            .Include(a => a.AnsweredByEmployee)
            .FirstOrDefaultAsync(cancellationToken);

        IsEditing = latest is not null;

        if (latest is not null)
        {
            AnsweredByDisplay = latest.AnsweredByEmployee?.FullName ?? "-";
            AnsweredAtDisplay = latest.AnsweredAt.HasValue ? FormatDate(latest.AnsweredAt.Value) : string.Empty;
        }

        return true;
    }

    /// <summary>Isi awal formulir saat layar pertama dibuka.</summary>
    private async Task FillDefaultsAsync(CancellationToken cancellationToken)
    {
        var latest = await LatestAnswerQuery(SelectedQuestion!.Id)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null)
        {
            Input.AnswerText = latest.AnswerText;
            Input.StatusId = SelectedQuestion.StatusId;
            return;
        }

        Input.StatusId = SuggestAnsweredStatusId() ?? SelectedQuestion.StatusId;
    }

    /// <summary>
    /// Pegawai yang mewakili akun yang sedang masuk. Tautannya bisa saja menunjuk
    /// pegawai yang sudah dihapus atau dinonaktifkan; kalau begitu dianggap tidak ada
    /// supaya jawaban tidak tercatat atas nama orang yang sudah tidak aktif.
    /// </summary>
    private async Task ResolveAnsweringEmployeeAsync(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminUserId))
        {
            return;
        }

        var linked = await _context.AdminUsers
            .AsNoTracking()
            .Where(u => u.Id == adminUserId && u.Employee != null && u.Employee.DeletedAt == null && u.Employee.IsActive)
            .Select(u => new { u.Employee!.Id, u.Employee.FullName })
            .FirstOrDefaultAsync(cancellationToken);

        if (linked is null)
        {
            return;
        }

        AnsweringEmployeeId = linked.Id;
        AnsweringEmployeeName = linked.FullName;
    }

    /// <summary>
    /// Status yang paling masuk akal saat sebuah pertanyaan baru dijawab. Memakai
    /// penggolongan yang sama dengan pewarnaan lencana, sehingga status yang di sini
    /// dipilihkan otomatis pasti yang di daftar tampil sebagai "sudah dijawab".
    /// Bila tidak ada yang tergolong begitu, kembalikan null supaya status lama
    /// dipertahankan alih-alih ditebak keliru.
    /// </summary>
    private Guid? SuggestAnsweredStatusId()
    {
        var match = StatusOptions.FirstOrDefault(choice =>
            QuestionStatusStyle.Classify(choice.Code, choice.Name) == QuestionStatusKind.Answered);

        return match?.Id;
    }

    private IQueryable<Models.Answer> LatestAnswerQuery(Guid questionId)
    {
        return _context.Answers
            .Where(a => a.QuestionId == questionId)
            // Jawaban tanpa tanggal diletakkan paling belakang tanpa memakai nilai
            // pengganti, karena DateTime.MinValue ditolak kolom timestamptz.
            .OrderByDescending(a => a.AnsweredAt.HasValue)
            .ThenByDescending(a => a.AnsweredAt);
    }

    private string FormatDate(DateTime utcValue) =>
        _appTimeZone.ToLocal(utcValue).ToString("dd MMMM yyyy, HH:mm", DisplayCulture);

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
