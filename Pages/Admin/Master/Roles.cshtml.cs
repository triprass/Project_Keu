using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure.Admin;
using Project_Keu.Infrastructure.Authorization;
using Project_Keu.Models;

namespace Project_Keu.Pages.Admin.Master;

[Authorize(Policy = AdminMenu.PolicyRoles)]
public class RolesModel : AdminPageModelBase
{
    private readonly AppDbContext _context;

    public RolesModel(AppDbContext context)
    {
        _context = context;
    }

    public sealed record PermissionOption(Guid Id, string Code, string Name, string Group);

    public sealed record Row(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        bool IsActive,
        int UserCount,
        int PermissionCount,
        IReadOnlyList<Guid> PermissionIds)
    {
        /// <summary>Peran bawaan sistem: selalu punya seluruh izin dan tidak boleh dihapus.</summary>
        public bool IsSuperAdmin => string.Equals(Code, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<Row> Items { get; private set; } = [];

    /// <summary>Izin dikelompokkan menurut kolom group_name agar daftarnya mudah dibaca.</summary>
    public IReadOnlyList<IGrouping<string, PermissionOption>> PermissionGroups { get; private set; } = [];

    [BindProperty]
    public RoleInput Input { get; set; } = new();

    public sealed class RoleInput
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Kode wajib diisi.")]
        [StringLength(50, ErrorMessage = "Kode maksimal 50 karakter.")]
        [RegularExpression("^[A-Za-z0-9_]+$", ErrorMessage = "Kode hanya boleh berisi huruf, angka, dan garis bawah.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nama wajib diisi.")]
        [StringLength(100, ErrorMessage = "Nama maksimal 100 karakter.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Keterangan maksimal 1000 karakter.")]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public List<Guid> PermissionIds { get; set; } = [];
    }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        Input.Code = Input.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        Input.Name = Input.Name?.Trim() ?? string.Empty;
        Input.Description = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim();

        var isNew = Input.Id is null || Input.Id == Guid.Empty;

        var duplicate = await _context.Roles
            .AsNoTracking()
            .AnyAsync(x => x.Id != (Input.Id ?? Guid.Empty) && x.Code.ToLower() == Input.Code.ToLower(), cancellationToken);

        if (duplicate)
        {
            ModelState.AddModelError("Input.Code", "Kode peran ini sudah dipakai.");
        }

        if (!ModelState.IsValid)
        {
            ReopenDialog = true;
            await LoadAsync(cancellationToken);
            return Page();
        }

        var now = DateTime.UtcNow;
        var validPermissionIds = await ValidPermissionIdsAsync(Input.PermissionIds, cancellationToken);

        if (isNew)
        {
            var entity = new Role
            {
                Id = Guid.NewGuid(),
                Code = Input.Code,
                Name = Input.Name,
                Description = Input.Description,
                IsActive = Input.IsActive,
                CreatedAt = now
            };

            _context.Roles.Add(entity);

            foreach (var permissionId in validPermissionIds)
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = entity.Id,
                    PermissionId = permissionId,
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Peran \"{entity.Name}\" berhasil dibuat.");
        }
        else
        {
            var entity = await _context.Roles
                .Include(x => x.RolePermissions)
                .FirstOrDefaultAsync(x => x.Id == Input.Id, cancellationToken);

            if (entity is null)
            {
                NotifyError("Peran tidak ditemukan.");
                return RedirectToList();
            }

            var wasSuperAdmin = string.Equals(entity.Code, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase);

            if (wasSuperAdmin)
            {
                // Kode SUPERADMIN dirujuk langsung oleh kode program saat memutuskan
                // siapa yang lolos seluruh pemeriksaan izin. Mengubah atau
                // menonaktifkannya akan melumpuhkan pengelolaan hak akses.
                if (!string.Equals(Input.Code, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
                {
                    NotifyError("Kode peran Super Administrator tidak dapat diubah.");
                    return RedirectToList();
                }

                if (!Input.IsActive)
                {
                    NotifyError("Peran Super Administrator tidak dapat dinonaktifkan.");
                    return RedirectToList();
                }
            }

            entity.Code = Input.Code;
            entity.Name = Input.Name;
            entity.Description = Input.Description;
            entity.IsActive = Input.IsActive;
            entity.UpdatedAt = now;

            SyncPermissions(entity, validPermissionIds, now);

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Peran \"{entity.Name}\" berhasil diperbarui.");
        }

        return RedirectToList();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.Roles
            .Include(x => x.RolePermissions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            NotifyError("Peran tidak ditemukan.");
            return RedirectToList();
        }

        if (string.Equals(entity.Code, AppRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase))
        {
            NotifyError("Peran Super Administrator adalah peran bawaan sistem dan tidak dapat dihapus.");
            return RedirectToList();
        }

        var inUse = await _context.AdminUserRoles.AnyAsync(x => x.RoleId == id, cancellationToken);

        if (inUse)
        {
            NotifyError($"Peran \"{entity.Name}\" masih dipakai akun admin. Lepaskan dulu dari akun-akun tersebut.");
            return RedirectToList();
        }

        _context.RolePermissions.RemoveRange(entity.RolePermissions);
        _context.Roles.Remove(entity);

        await _context.SaveChangesAsync(cancellationToken);
        Notify($"Peran \"{entity.Name}\" berhasil dihapus.");

        return RedirectToList();
    }

    private async Task<List<Guid>> ValidPermissionIdsAsync(IEnumerable<Guid> requested, CancellationToken cancellationToken)
    {
        var wanted = requested.Distinct().ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        return await _context.Permissions
            .AsNoTracking()
            .Where(p => wanted.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    private void SyncPermissions(Role entity, List<Guid> permissionIds, DateTime now)
    {
        var existing = entity.RolePermissions.ToList();

        var removed = existing.Where(x => !permissionIds.Contains(x.PermissionId)).ToList();
        if (removed.Count > 0)
        {
            _context.RolePermissions.RemoveRange(removed);
        }

        var currentIds = existing.Select(x => x.PermissionId).ToHashSet();

        foreach (var permissionId in permissionIds.Where(pid => !currentIds.Contains(pid)))
        {
            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = entity.Id,
                PermissionId = permissionId,
                CreatedAt = now
            });
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var permissions = await _context.Permissions
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.GroupName)
            .ThenBy(p => p.Code)
            .Select(p => new PermissionOption(p.Id, p.Code, p.Name, p.GroupName ?? "Lainnya"))
            .ToListAsync(cancellationToken);

        PermissionGroups = permissions.GroupBy(p => p.Group).ToList();

        var query = _context.Roles.AsNoTracking();

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

        var rows = await query
            .OrderBy(x => x.Name)
            .Skip(RowOffset)
            .Take(PageSize)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.Description,
                x.IsActive,
                UserCount = x.AdminUserRoles.Count,
                PermissionIds = x.RolePermissions.Select(rp => rp.PermissionId).ToList()
            })
            .ToListAsync(cancellationToken);

        Items = rows
            .Select(x => new Row(
                x.Id, x.Code, x.Name, x.Description, x.IsActive,
                x.UserCount, x.PermissionIds.Count, x.PermissionIds))
            .ToList();
    }
}
