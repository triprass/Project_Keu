using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure.Admin;
using Project_Keu.Models;

namespace Project_Keu.Pages.Admin.Master;

[Authorize(Policy = AdminMenu.PolicyCategories)]
public class QuestionsModel : AdminPageModelBase
{
    private readonly AppDbContext _context;

    public QuestionsModel(AppDbContext context)
    {
        _context = context;
    }

    public sealed record Row(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        bool IsActive,
        int QuestionCount);

    public IReadOnlyList<Row> Items { get; private set; } = [];

    [BindProperty]
    public CategoryInput Input { get; set; } = new();

    public sealed class CategoryInput
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Kode wajib diisi.")]
        [StringLength(20, ErrorMessage = "Kode maksimal 20 karakter.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nama wajib diisi.")]
        [StringLength(200, ErrorMessage = "Nama maksimal 200 karakter.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Keterangan maksimal 1000 karakter.")]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Input.Code = Input.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        Input.Name = Input.Name?.Trim() ?? string.Empty;
        Input.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();

        var duplicate = await _context.QuestionCategories
            .AsNoTracking()
            .AnyAsync(x => x.Id != (Input.Id ?? Guid.Empty) && x.Code.ToLower() == Input.Code.ToLower(), cancellationToken);

        if (duplicate)
        {
            ModelState.AddModelError("Input.Code", "Kode ini sudah dipakai kategori lain.");
        }

        if (!ModelState.IsValid)
        {
            ReopenDialog = true;
            await LoadAsync(cancellationToken);
            return Page();
        }

        var now = DateTime.UtcNow;

        if (Input.Id is null || Input.Id == Guid.Empty)
        {
            _context.QuestionCategories.Add(new QuestionCategory
            {
                Id = Guid.NewGuid(),
                Code = Input.Code,
                Name = Input.Name,
                Description = Input.Description,
                IsActive = Input.IsActive,
                CreatedBy = CurrentUserName,
                CreatedAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Kategori pertanyaan \"{Input.Name}\" berhasil ditambahkan.");
        }
        else
        {
            var entity = await _context.QuestionCategories
                .FirstOrDefaultAsync(x => x.Id == Input.Id, cancellationToken);

            if (entity is null)
            {
                NotifyError("Kategori pertanyaan tidak ditemukan.");
                return RedirectToList();
            }

            entity.Code = Input.Code;
            entity.Name = Input.Name;
            entity.Description = Input.Description;
            entity.IsActive = Input.IsActive;
            entity.UpdatedBy = CurrentUserName;
            entity.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Kategori pertanyaan \"{Input.Name}\" berhasil diperbarui.");
        }

        return RedirectToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.QuestionCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            NotifyError("Kategori pertanyaan tidak ditemukan.");
            return RedirectToList();
        }

        // Tabel ini tidak punya kolom penghapusan lunak, sedangkan pertanyaan
        // merujuknya lewat kunci asing. Menghapusnya akan menggagalkan perintah di
        // basis data, jadi hubungannya diperiksa lebih dulu agar pesannya jelas.
        var inUse = await _context.Questions.AnyAsync(q => q.CategoryId == id, cancellationToken);

        if (inUse)
        {
            NotifyError($"\"{entity.Name}\" masih dipakai oleh pertanyaan yang sudah ada. Nonaktifkan saja agar tidak bisa dipilih lagi.");
            return RedirectToList();
        }

        _context.QuestionCategories.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        Notify($"Kategori pertanyaan \"{entity.Name}\" berhasil dihapus.");

        return RedirectToList();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var query = _context.QuestionCategories.AsNoTracking();

        TotalUnfiltered = await query.CountAsync(cancellationToken);

        var keyword = NormalizedSearch();

        if (keyword is not null)
        {
            var pattern = ContainsPattern(keyword);

            query = query.Where(x =>
                EF.Functions.ILike(x.Code, pattern, LikeEscape) ||
                EF.Functions.ILike(x.Name, pattern, LikeEscape) ||
                (x.Description != null && EF.Functions.ILike(x.Description, pattern, LikeEscape)));
        }

        TotalItems = await query.CountAsync(cancellationToken);
        ClampPage();

        Items = await query
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Skip(RowOffset)
            .Take(PageSize)
            .Select(x => new Row(
                x.Id,
                x.Code,
                x.Name,
                x.Description,
                x.IsActive,
                x.Questions.Count))
            .ToListAsync(cancellationToken);
    }
}
