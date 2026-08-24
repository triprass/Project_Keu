using DocumentFormat.OpenXml.Office.SpreadSheetML.Y2023.MsForms;
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure;
using Project_Keu.Infrastructure.Admin;
using Project_Keu.Models;

namespace Project_Keu.Pages.Admin.Master;

[Authorize(Policy = AdminMenu.PolicyAnswer)]
public class QuestionsModel : AdminPageModelBase
{
    private readonly AppDbContext _context;
    private readonly AppTimeZone _timeZone;

    public QuestionsModel(AppDbContext context, AppTimeZone timeZone)
    {
        _context = context;
        _timeZone = timeZone;  
    }

    public sealed record Row(
        Guid Id,
        string QuestionNo,
        string Title,
        string QuestionText,
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

    public IReadOnlyList<Row> Items { get; private set; } = [];

    [BindProperty]
    public QuestionsInput Input { get; set; } = new();

    public sealed class QuestionsInput
    {
        public Guid Id { get; set; }
        public string? QuestionNo { get; set; }
    }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.Questions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            NotifyError("Pertanyaan tidak ditemukan.");
            return RedirectToList();
        }

        // Tabel ini tidak punya kolom penghapusan lunak, sedangkan pertanyaan
        // merujuknya lewat kunci asing. Menghapusnya akan menggagalkan perintah di
        // basis data, jadi hubungannya diperiksa lebih dulu agar pesannya jelas.
        var inUse = await _context.Questions.AnyAsync(q => q.CategoryId == id, cancellationToken);

        if (inUse)
        {
            NotifyError($"\"{entity.QuestionText}\" masih dipakai oleh pertanyaan yang sudah ada. Nonaktifkan saja agar tidak bisa dipilih lagi.");
            return RedirectToList();
        }

        _context.Questions.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        Notify($"Pertanyaan \"{entity.CreatedByEmployeeNavigation}\" berhasil dihapus.");

        return RedirectToList();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var query = _context.Questions.AsNoTracking();

        TotalUnfiltered = await query.CountAsync(cancellationToken);

        var keyword = NormalizedSearch();

        if (keyword is not null)
        {
            var pattern = ContainsPattern(keyword);

            query = query.Where(x =>
                EF.Functions.ILike(x.QuestionText, pattern, LikeEscape));
        }

        TotalItems = await query.CountAsync(cancellationToken);
        ClampPage();

        Items = await query
            .OrderBy(x => x.QuestionNo)
            .ThenBy(x => x.Id)
            .Skip(RowOffset)
            .Take(PageSize)
            .Select(x => new Row(
                x.Id,
                x.QuestionNo ?? "-",
                x.Title,
                x.QuestionText,
                x.CreatedByEmployeeNavigation != null ? x.CreatedByEmployeeNavigation.FullName : "-",
                x.Status != null ? x.Status.Name : "-",
                x.Status != null ? (x.Status.Code ?? string.Empty) : string.Empty,
                x.Status != null ? x.Status.Color : null,
                x.Answers.Any(),
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public string FormatDate(DateTime utcValue) =>
        _timeZone.ToLocal(utcValue).ToString("dd MMM yyyy HH:mm");

}

