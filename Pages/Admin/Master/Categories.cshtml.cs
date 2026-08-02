using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure.Admin;
using Project_Keu.Models;

namespace Project_Keu.Pages.Admin.Master;

[Authorize(Policy = AdminMenu.PolicyCategories)]
public class CategoriesModel : AdminPageModelBase
{
    private readonly AppDbContext _context;

    public CategoriesModel(AppDbContext context)
    {
        _context = context;
    }

    public sealed record Row(Guid Id, string Code, string Name, string? Description, bool IsActive, DateTime CreatedAt);

    public IReadOnlyList<Row> Items { get; private set; } = [];

    [BindProperty]
    public CategoryInput Input { get; set; } = new();

    public sealed class CategoryInput
    {
        /// <summary>Kosong berarti data baru. Dikirim di badan POST, tidak pernah lewat URL.</summary>
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Kode wajib diisi.")]
        [StringLength(20, ErrorMessage = "Kode maksimal 20 karakter.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nama wajib diisi.")]
        [StringLength(150, ErrorMessage = "Nama maksimal 150 karakter.")]
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

        // Kode dibandingkan tanpa membedakan huruf besar-kecil supaya "TRV" dan "trv"
        // tidak bisa berdampingan dan membingungkan saat dipakai di laporan.
        var duplicate = await _context.Categories
            .AsNoTracking()
            .AnyAsync(x => x.DeletedAt == null
                        && x.Id != (Input.Id ?? Guid.Empty)
                        && x.Code.ToLower() == Input.Code.ToLower(), cancellationToken);

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
            _context.Categories.Add(new Category
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
            Notify($"Kategori \"{Input.Name}\" berhasil ditambahkan.");
        }
        else
        {
            var entity = await _context.Categories
                .FirstOrDefaultAsync(x => x.Id == Input.Id && x.DeletedAt == null, cancellationToken);

            if (entity is null)
            {
                NotifyError("Kategori tidak ditemukan atau sudah dihapus.");
                return RedirectToList();
            }

            entity.Code = Input.Code;
            entity.Name = Input.Name;
            entity.Description = Input.Description;
            entity.IsActive = Input.IsActive;
            entity.UpdatedBy = CurrentUserName;
            entity.UpdatedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Kategori \"{Input.Name}\" berhasil diperbarui.");
        }

        return RedirectToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.Categories
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, cancellationToken);

        if (entity is null)
        {
            NotifyError("Kategori tidak ditemukan atau sudah dihapus.");
            return RedirectToList();
        }

        // Penghapusan bersifat lunak: baris tetap ada agar data lama yang pernah
        // merujuk kategori ini tidak kehilangan konteksnya.
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedBy = CurrentUserName;
        entity.IsActive = false;

        await _context.SaveChangesAsync(cancellationToken);
        Notify($"Kategori \"{entity.Name}\" berhasil dihapus.");

        return RedirectToList();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var query = _context.Categories.AsNoTracking().Where(x => x.DeletedAt == null);

        TotalUnfiltered = await query.CountAsync(cancellationToken);

        var keyword = NormalizedSearch();

        if (keyword is not null)
        {
            var pattern = ContainsPattern(keyword);

            // ILike, bukan Like: pencarian di PostgreSQL harus tetap menemukan hasil
            // meski pengguna mengetik dengan huruf besar-kecil yang berbeda.
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
            .Select(x => new Row(x.Id, x.Code, x.Name, x.Description, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
