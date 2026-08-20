using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Project_Keu.Data;
using Project_Keu.Infrastructure;
using Project_Keu.Infrastructure.Admin;
using Project_Keu.Infrastructure.Authorization;
using Project_Keu.Infrastructure.Notifications;

namespace Project_Keu.Services.Notifications;

/// <summary>
/// Menyusun isi pemberitahuan dan menentukan penerimanya. Dijalankan pekerja latar
/// belakang, bukan di dalam request.
/// </summary>
public sealed class NotificationDispatcher
{
    /// <summary>Kutipan pertanyaan/jawaban dipotong supaya satu pesan WhatsApp tetap terbaca.</summary>
    private const int MaxQuotedLength = 700;

    private static readonly CultureInfo DisplayCulture = CreateDisplayCulture();

    private readonly AppDbContext _context;
    private readonly IWhatsAppSender _sender;
    private readonly AppTimeZone _appTimeZone;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        AppDbContext context,
        IWhatsAppSender sender,
        AppTimeZone appTimeZone,
        IOptions<NotificationOptions> options,
        ILogger<NotificationDispatcher> logger)
    {
        _context = context;
        _sender = sender;
        _appTimeZone = appTimeZone;
        _options = options.Value;
        _logger = logger;
    }

    public async Task HandleAsync(NotificationJob job, CancellationToken cancellationToken)
    {
        var question = await _context.Questions
            .AsNoTracking()
            .Include(q => q.Category)
            .Include(q => q.Status)
            .Include(q => q.CreatedByEmployeeNavigation)
            .FirstOrDefaultAsync(q => q.Id == job.QuestionId, cancellationToken);

        if (question is null)
        {
            _logger.LogWarning("Pertanyaan {QuestionId} tidak ditemukan saat menyiapkan pemberitahuan.", job.QuestionId);
            return;
        }

        switch (job.Kind)
        {
            case NotificationKind.QuestionCreated:
                await NotifyAdminsAsync(question, cancellationToken);
                break;

            case NotificationKind.QuestionAnswered:
                await NotifyAskerAsync(question, cancellationToken);
                break;
        }
    }

    // ---------------------------------------------------------------- pertanyaan baru

    private async Task NotifyAdminsAsync(Models.Question question, CancellationToken cancellationToken)
    {
        var recipients = await ResolveAdminChatIdsAsync(cancellationToken);

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "Tidak ada penerima untuk pemberitahuan pertanyaan {QuestionNo}: belum ada pengelola " +
                "berhak menjawab yang nomor teleponnya terisi, dan Notifications:AdminRecipients kosong.",
                question.QuestionNo);
            return;
        }

        var message = BuildQuestionCreatedMessage(question);

        foreach (var chatId in recipients)
        {
            await _sender.SendTextAsync(chatId, message, cancellationToken);
        }
    }

    private string BuildQuestionCreatedMessage(Models.Question question)
    {
        var body = new StringBuilder();

        body.AppendLine("*Pertanyaan Baru - Pilar Keuangan*");
        body.AppendLine();
        AppendField(body, "Nomor", Fallback(question.QuestionNo));
        AppendField(body, "Kategori", Fallback(question.Category?.Name));
        AppendField(body, "Pegawai", Fallback(question.CreatedByEmployeeNavigation?.FullName));
        AppendField(body, "Waktu", FormatDate(question.CreatedAt));
        body.AppendLine();
        body.AppendLine("Pertanyaan:");
        body.AppendLine(Quote(question.QuestionText));
        body.AppendLine();
        body.Append("Mohon ditindaklanjuti melalui panel administrasi.");

        AppendPortalUrl(body);

        return body.ToString();
    }

    // -------------------------------------------------------------- jawaban terkirim

    private async Task NotifyAskerAsync(Models.Question question, CancellationToken cancellationToken)
    {
        var chatId = WhatsAppChatId.FromPhoneNumber(
            question.CreatedByEmployeeNavigation?.PhoneNumber,
            _options.DefaultCountryCode);

        if (chatId is null)
        {
            _logger.LogWarning(
                "Jawaban untuk {QuestionNo} tidak diberitahukan: nomor telepon pegawai {Employee} kosong atau tidak sah.",
                question.QuestionNo,
                question.CreatedByEmployeeNavigation?.FullName ?? "(tidak diketahui)");
            return;
        }

        var answer = await _context.Answers
            .AsNoTracking()
            .Where(a => a.QuestionId == question.Id)
            // Urutan sama dengan halaman detail dan halaman menjawab, supaya yang
            // dikirimkan adalah jawaban yang sama dengan yang dilihat pengelola.
            .OrderByDescending(a => a.AnsweredAt.HasValue)
            .ThenByDescending(a => a.AnsweredAt)
            .Select(a => new
            {
                a.AnswerText,
                a.AnsweredAt,
                AnsweredByName = a.AnsweredByEmployee != null ? a.AnsweredByEmployee.FullName : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (answer is null)
        {
            _logger.LogWarning("Pertanyaan {QuestionNo} belum punya jawaban saat pemberitahuan disiapkan.", question.QuestionNo);
            return;
        }

        var body = new StringBuilder();

        body.AppendLine("*Pertanyaan Anda Telah Dijawab*");
        body.AppendLine();
        AppendField(body, "Nomor", Fallback(question.QuestionNo));
        AppendField(body, "Kategori", Fallback(question.Category?.Name));
        AppendField(body, "Status", Fallback(question.Status?.Name));
        AppendField(body, "Penjawab", Fallback(answer.AnsweredByName));

        if (answer.AnsweredAt.HasValue)
        {
            AppendField(body, "Waktu", FormatDate(answer.AnsweredAt.Value));
        }

        body.AppendLine();
        body.AppendLine("Pertanyaan Anda:");
        body.AppendLine(Quote(question.QuestionText));
        body.AppendLine();
        body.AppendLine("Jawaban:");
        body.Append(Quote(answer.AnswerText));

        AppendPortalUrl(body);

        await _sender.SendTextAsync(chatId, body.ToString(), cancellationToken);
    }

    // ------------------------------------------------------------------- penerima

    /// <summary>
    /// Nomor pengelola yang memang berwenang menjawab: akun aktif yang punya izin
    /// <c>questions.answer</c> lewat peran aktif, atau berperan SUPERADMIN yang selalu
    /// lolos pemeriksaan izin. Nomornya diambil dari pegawai yang tertaut ke akun itu,
    /// karena tabel akun sendiri tidak menyimpan nomor telepon.
    /// </summary>
    private async Task<List<string>> ResolveAdminChatIdsAsync(CancellationToken cancellationToken)
    {
        var phoneNumbers = await _context.AdminUsers
            .AsNoTracking()
            .Where(u => u.IsActive
                && u.Employee != null
                && u.Employee.DeletedAt == null
                && u.Employee.IsActive
                && u.Employee.PhoneNumber != null
                && u.AdminUserRoles.Any(ur =>
                    ur.Role != null &&
                    ur.Role.IsActive &&
                    (ur.Role.Code == AppRoles.SuperAdmin ||
                     ur.Role.RolePermissions.Any(rp =>
                         rp.Permission != null &&
                         rp.Permission.IsActive &&
                         rp.Permission.Code == AdminMenu.PolicyAnswer))))
            .Select(u => u.Employee!.PhoneNumber!)
            .ToListAsync(cancellationToken);

        // Nomor tambahan dari konfigurasi digabung, bukan menggantikan, supaya nomor
        // piket tetap menerima meski seluruh akun pengelola belum mengisi teleponnya.
        var candidates = phoneNumbers.Concat(_options.AdminRecipients);

        // Satu orang bisa memegang lebih dari satu peran; tanpa penyaringan ini ia
        // menerima pesan yang sama beberapa kali.
        return candidates
            .Select(phone => WhatsAppChatId.FromPhoneNumber(phone, _options.DefaultCountryCode))
            .Where(chatId => chatId is not null)
            .Select(chatId => chatId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // -------------------------------------------------------------------- penyusun

    private void AppendPortalUrl(StringBuilder body)
    {
        if (string.IsNullOrWhiteSpace(_options.PortalUrl))
        {
            return;
        }

        body.AppendLine();
        body.AppendLine();
        body.Append(_options.PortalUrl.Trim());
    }

    private static void AppendField(StringBuilder body, string label, string value)
    {
        body.Append(label).Append(": ").AppendLine(value);
    }

    private static string Fallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

    private static string Quote(string? value)
    {
        var text = value?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return "-";
        }

        return text.Length > MaxQuotedLength
            ? text[..MaxQuotedLength] + "..."
            : text;
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
