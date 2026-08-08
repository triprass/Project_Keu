using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure;
using Project_Keu.Infrastructure.Admin;
using Project_Keu.Models;

namespace Project_Keu.Pages.Admin.Master;

[Authorize(Policy = AdminMenu.PolicyStatuses)]
public class QuestionStatusesModel : AdminPageModelBase
{
    private readonly AppDbContext _context;

    public QuestionStatusesModel(AppDbContext context)
    {
        _context = context;
    }

    public sealed record Row(Guid Id, string Code, string Name, string? Color, bool IsActive, int QuestionCount);

    public IReadOnlyList<Row> Items { get; private set; } = [];

    [BindProperty]
    public StatusInput Input { get; set; } = new();

    public sealed class StatusInput
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Kode wajib diisi.")]
        [StringLength(20, ErrorMessage = "Kode maksimal 20 karakter.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nama wajib diisi.")]
        [StringLength(50, ErrorMessage = "Nama maksimal 50 karakter.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(30, ErrorMessage = "Warna maksimal 30 karakter.")]
        public string? Color { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Warna yang aman dipakai pada atribut style. Penyaringnya dipakai bersama
    /// dengan pewarnaan lencana di daftar pertanyaan, jadi satu aturan saja.
    /// </summary>
    public static string SafeColor(string? value) => QuestionStatusStyle.SafeColor(value);

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Input.Code = Input.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        Input.Name = Input.Name?.Trim() ?? string.Empty;
        Input.Color = string.IsNullOrWhiteSpace(Input.Color) ? null : Input.Color.Trim();

        if (Input.Color is not null && !QuestionStatusStyle.HasUsableColor(Input.Color))
        {
            ModelState.AddModelError("Input.Color", "Isi dengan kode heksadesimal seperti #16a34a atau nama warna seperti green.");
        }

        var duplicate = await _context.QuestionStatuses
            .AsNoTracking()
            .AnyAsync(x => x.Id != (Input.Id ?? Guid.Empty) && x.Code.ToLower() == Input.Code.ToLower(), cancellationToken);

        if (duplicate)
        {
            ModelState.AddModelError("Input.Code", "Kode ini sudah dipakai status lain.");
        }

        if (!ModelState.IsValid)
        {
            ReopenDialog = true;
            await LoadAsync(cancellationToken);
            return Page();
        }

        if (Input.Id is null || Input.Id == Guid.Empty)
        {
            _context.QuestionStatuses.Add(new QuestionStatus
            {
                Id = Guid.NewGuid(),
                Code = Input.Code,
                Name = Input.Name,
                Color = Input.Color,
                IsActive = Input.IsActive
            });

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Status \"{Input.Name}\" berhasil ditambahkan.");
        }
        else
        {
            var entity = await _context.QuestionStatuses.FirstOrDefaultAsync(x => x.Id == Input.Id, cancellationToken);

            if (entity is null)
            {
                NotifyError("Status tidak ditemukan.");
                return RedirectToList();
            }

            entity.Code = Input.Code;
            entity.Name = Input.Name;
            entity.Color = Input.Color;
            entity.IsActive = Input.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Status \"{Input.Name}\" berhasil diperbarui.");
        }

        return RedirectToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.QuestionStatuses.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            NotifyError("Status tidak ditemukan.");
            return RedirectToList();
        }

        var inUse = await _context.Questions.AnyAsync(q => q.StatusId == id, cancellationToken);

        if (inUse)
        {
            NotifyError($"Status \"{entity.Name}\" masih melekat pada pertanyaan yang ada. Nonaktifkan saja agar tidak dipakai lagi.");
            return RedirectToList();
        }

        _context.QuestionStatuses.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        Notify($"Status \"{entity.Name}\" berhasil dihapus.");

        return RedirectToList();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var query = _context.QuestionStatuses.AsNoTracking();

        TotalUnfiltered = await query.CountAsync(cancellationToken);

        var keyword = NormalizedSearch();

        if (keyword is not null)
        {
            var pattern = ContainsPattern(keyword);

            query = query.Where(x =>
                EF.Functions.ILike(x.Code, pattern, LikeEscape) ||
                EF.Functions.ILike(x.Name, pattern, LikeEscape));
        }

        TotalItems = await query.CountAsync(cancellationToken);
        ClampPage();

        Items = await query
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Skip(RowOffset)
            .Take(PageSize)
            .Select(x => new Row(x.Id, x.Code, x.Name, x.Color, x.IsActive, x.Questions.Count))
            .ToListAsync(cancellationToken);
    }
}
