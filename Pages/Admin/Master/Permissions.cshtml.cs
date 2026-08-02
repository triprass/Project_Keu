using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure.Admin;
using Project_Keu.Models;

namespace Project_Keu.Pages.Admin.Master;

[Authorize(Policy = AdminMenu.PolicyRoles)]
public class PermissionsModel : AdminPageModelBase
{
    private readonly AppDbContext _context;

    public PermissionsModel(AppDbContext context)
    {
        _context = context;
    }

    public sealed record Row(
        Guid Id,
        string Code,
        string Name,
        string? GroupName,
        string? Description,
        bool IsActive,
        int RoleCount,
        IReadOnlyList<string> RoleNames);

    public IReadOnlyList<Row> Items { get; private set; } = [];

    /// <summary>Kelompok yang sudah dipakai, ditawarkan sebagai saran isian.</summary>
    public IReadOnlyList<string> KnownGroups { get; private set; } = [];

    [BindProperty]
    public PermissionInput Input { get; set; } = new();

    public sealed class PermissionInput
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Kode wajib diisi.")]
        [StringLength(100, ErrorMessage = "Kode maksimal 100 karakter.")]
        [RegularExpression("^[a-z0-9]+(\\.[a-z0-9]+)+$",
            ErrorMessage = "Kode harus bergaya \"grup.aksi\" dengan huruf kecil, mis. reports.view.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nama wajib diisi.")]
        [StringLength(150, ErrorMessage = "Nama maksimal 150 karakter.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(50, ErrorMessage = "Kelompok maksimal 50 karakter.")]
        public string? GroupName { get; set; }

        [StringLength(1000, ErrorMessage = "Keterangan maksimal 1000 karakter.")]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        // Kode izin dibandingkan apa adanya oleh pemeriksa otorisasi, jadi bentuknya
        // diseragamkan menjadi huruf kecil di satu tempat: di sini.
        Input.Code = Input.Code?.Trim().ToLowerInvariant() ?? string.Empty;
        Input.Name = Input.Name?.Trim() ?? string.Empty;
        Input.GroupName = string.IsNullOrWhiteSpace(Input.GroupName) ? null : Input.GroupName.Trim();
        Input.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();

        var duplicate = await _context.Permissions
            .AsNoTracking()
            .AnyAsync(x => x.Id != (Input.Id ?? Guid.Empty) && x.Code.ToLower() == Input.Code, cancellationToken);

        if (duplicate)
        {
            ModelState.AddModelError("Input.Code", "Kode izin ini sudah ada.");
        }

        if (!ModelState.IsValid)
        {
            ReopenDialog = true;
            await LoadAsync(cancellationToken);
            return Page();
        }

        if (Input.Id is null || Input.Id == Guid.Empty)
        {
            _context.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Code = Input.Code,
                Name = Input.Name,
                GroupName = Input.GroupName,
                Description = Input.Description,
                IsActive = Input.IsActive,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Izin \"{Input.Code}\" berhasil ditambahkan.");
        }
        else
        {
            var entity = await _context.Permissions.FirstOrDefaultAsync(x => x.Id == Input.Id, cancellationToken);

            if (entity is null)
            {
                NotifyError("Izin tidak ditemukan.");
                return RedirectToList();
            }

            entity.Code = Input.Code;
            entity.Name = Input.Name;
            entity.GroupName = Input.GroupName;
            entity.Description = Input.Description;
            entity.IsActive = Input.IsActive;

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Izin \"{entity.Code}\" berhasil diperbarui.");
        }

        return RedirectToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.Permissions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            NotifyError("Izin tidak ditemukan.");
            return RedirectToList();
        }

        var inUse = await _context.RolePermissions.AnyAsync(x => x.PermissionId == id, cancellationToken);

        if (inUse)
        {
            NotifyError($"Izin \"{entity.Code}\" masih terpasang pada peran. Lepaskan dulu dari peran-peran tersebut.");
            return RedirectToList();
        }

        _context.Permissions.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        Notify($"Izin \"{entity.Code}\" berhasil dihapus.");

        return RedirectToList();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        KnownGroups = await _context.Permissions
            .AsNoTracking()
            .Where(p => p.GroupName != null)
            .Select(p => p.GroupName!)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

        var query = _context.Permissions.AsNoTracking();

        TotalUnfiltered = await query.CountAsync(cancellationToken);

        var keyword = NormalizedSearch();

        if (keyword is not null)
        {
            var pattern = ContainsPattern(keyword);

            query = query.Where(x =>
                EF.Functions.ILike(x.Code, pattern, LikeEscape) ||
                EF.Functions.ILike(x.Name, pattern, LikeEscape) ||
                (x.GroupName != null && EF.Functions.ILike(x.GroupName, pattern, LikeEscape)));
        }

        TotalItems = await query.CountAsync(cancellationToken);
        ClampPage();

        var rows = await query
            .OrderBy(x => x.GroupName)
            .ThenBy(x => x.Code)
            .Skip(RowOffset)
            .Take(PageSize)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.GroupName,
                x.Description,
                x.IsActive,
                RoleNames = x.RolePermissions.Where(rp => rp.Role != null).Select(rp => rp.Role!.Name).ToList()
            })
            .ToListAsync(cancellationToken);

        Items = rows
            .Select(x => new Row(
                x.Id, x.Code, x.Name, x.GroupName, x.Description, x.IsActive,
                x.RoleNames.Count, x.RoleNames))
            .ToList();
    }
}
