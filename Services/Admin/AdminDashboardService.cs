using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure;

namespace Project_Keu.Services.Admin;

/// <summary>Angka ringkasan dan aktivitas terakhir untuk beranda panel administrasi.</summary>
public sealed class AdminDashboardService
{
    private readonly AppDbContext _context;

    public AdminDashboardService(AppDbContext context)
    {
        _context = context;
    }

    public sealed record RecentQuestion(
        Guid Id,
        string QuestionNo,
        string Title,
        string EmployeeName,
        string StatusName,
        string StatusCode,
        string? StatusColor,
        bool HasAnswer,
        DateTime CreatedAtUtc)
    {
        /// <summary>Golongan status untuk pewarnaan lencana, dengan aturan yang sama seperti daftar pertanyaan.</summary>
        public QuestionStatusKind StatusKind => QuestionStatusStyle.Classify(StatusCode, StatusName);
    }

    public sealed record StatusTally(string StatusName, int Total);

    public sealed record Snapshot(
        int TotalQuestions,
        int AnsweredQuestions,
        int PendingQuestions,
        int QuestionsLast7Days,
        int ActiveEmployees,
        int ActiveCategories,
        int ActiveAdmins,
        IReadOnlyList<StatusTally> ByStatus,
        IReadOnlyList<RecentQuestion> Recent);

    public async Task<Snapshot> GetAsync(DateTime sevenDaysAgoUtc, CancellationToken cancellationToken = default)
    {
        var questions = _context.Questions.AsNoTracking();

        var totalQuestions = await questions.CountAsync(cancellationToken);

        // "Terjawab" ditentukan oleh keberadaan baris jawaban, bukan oleh nama status,
        // supaya angkanya tetap benar meski nama status diubah lewat halaman master.
        var answeredQuestions = await questions.CountAsync(q => q.Answers.Any(), cancellationToken);

        var questionsLast7Days = await questions.CountAsync(q => q.CreatedAt >= sevenDaysAgoUtc, cancellationToken);

        // Kunci pengelompokan harus berupa ekspresi sederhana. Percabangan di dalam
        // GroupBy tidak punya padanan SQL dan membuat kuerinya gagal diterjemahkan,
        // jadi penggantian nama untuk status kosong dilakukan setelah data terambil.
        var statusRows = await questions
            .GroupBy(q => q.Status!.Name)
            .Select(g => new { Name = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);

        var byStatus = statusRows
            .Select(x => new StatusTally(string.IsNullOrWhiteSpace(x.Name) ? "Tanpa status" : x.Name, x.Total))
            .OrderByDescending(x => x.Total)
            .ToList();

        var recent = await questions
            .Where(q => q.UpdatedAt == null)
            .OrderBy(q => q.CreatedAt)
            .Take(6)
            .Select(q => new RecentQuestion(
                q.Id,
                q.QuestionNo ?? "-",
                q.Title,
                q.CreatedByEmployeeNavigation != null ? q.CreatedByEmployeeNavigation.FullName : "-",
                q.Status != null ? q.Status.Name : "-",
                q.Status != null ? (q.Status.Code ?? string.Empty) : string.Empty,
                q.Status != null ? q.Status.Color : null,
                q.Answers.Any(),
                q.CreatedAt))
            .ToListAsync(cancellationToken);

        var activeEmployees = await _context.Employees
            .AsNoTracking()
            .CountAsync(e => e.IsActive && e.DeletedAt == null, cancellationToken);

        var activeCategories = await _context.QuestionCategories
            .AsNoTracking()
            .CountAsync(c => c.IsActive, cancellationToken);

        var activeAdmins = await _context.AdminUsers
            .AsNoTracking()
            .CountAsync(a => a.IsActive, cancellationToken);

        return new Snapshot(
            totalQuestions,
            answeredQuestions,
            Math.Max(0, totalQuestions - answeredQuestions),
            questionsLast7Days,
            activeEmployees,
            activeCategories,
            activeAdmins,
            byStatus,
            recent);
    }
}
