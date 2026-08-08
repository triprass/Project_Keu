using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure;
using Project_Keu.Infrastructure.Admin;
using Project_Keu.Infrastructure.Authorization;
using Project_Keu.Models;

namespace Project_Keu.Pages.Admin.Master;

[Authorize(Policy = AdminMenu.PolicyAdminUsers)]
public class AdminUsersModel : AdminPageModelBase
{
    /// <summary>Panjang minimum kata sandi baru yang dibuat lewat halaman ini.</summary>
    public const int MinPasswordLength = 10;

    private readonly AppDbContext _context;

    public AdminUsersModel(AppDbContext context)
    {
        _context = context;
    }

    public sealed record RoleOption(Guid Id, string Code, string Name, bool IsActive);

    public sealed record EmployeeOption(Guid Id, string FullName, string? Nip);

    public sealed record Row(
        Guid Id,
        string Username,
        string FullName,
        string? Email,
        Guid? EmployeeId,
        string? EmployeeName,
        bool IsActive,
        DateTime? LastLoginAt,
        DateTime? LockedUntil,
        IReadOnlyList<string> RoleNames,
        IReadOnlyList<Guid> RoleIds);

    public IReadOnlyList<Row> Items { get; private set; } = [];

    public IReadOnlyList<RoleOption> Roles { get; private set; } = [];

    /// <summary>Pilihan pegawai untuk menautkan akun; tautan inilah yang dipakai saat mencatat jawaban.</summary>
    public IReadOnlyList<EmployeeOption> Employees { get; private set; } = [];

    /// <summary>Nama dialog yang harus terbuka kembali setelah validasi gagal.</summary>
    public string? ReopenDialogName { get; private set; }

    // Kedua model ini TIDAK memakai [BindProperty], melainkan diterima sebagai
    // parameter handler. Dengan [BindProperty], keduanya ikut terikat dan
    // tervalidasi pada setiap POST: menyimpan akun akan gagal karena kolom wajib
    // milik dialog ganti kata sandi kosong, dan sebaliknya. Sebagai parameter,
    // masing-masing hanya divalidasi pada handler yang memang memakainya.
    public AccountInput Input { get; set; } = new();

    public PasswordInput Password { get; set; } = new();

    public Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    public sealed class AccountInput
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "Nama pengguna wajib diisi.")]
        [StringLength(100, ErrorMessage = "Nama pengguna maksimal 100 karakter.")]
        [RegularExpression("^[a-zA-Z0-9._-]+$",
            ErrorMessage = "Nama pengguna hanya boleh berisi huruf, angka, titik, garis bawah, dan tanda hubung.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nama lengkap wajib diisi.")]
        [StringLength(150, ErrorMessage = "Nama lengkap maksimal 150 karakter.")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Format surel tidak sesuai.")]
        [StringLength(150, ErrorMessage = "Surel maksimal 150 karakter.")]
        public string? Email { get; set; }

        /// <summary>
        /// Pegawai yang diwakili akun ini. Diperlukan agar pemiliknya bisa menjawab
        /// pertanyaan, karena jawaban tercatat atas nama pegawai.
        /// </summary>
        public Guid? EmployeeId { get; set; }

        /// <summary>Wajib diisi saat membuat akun baru, diabaikan saat mengubah akun.</summary>
        [StringLength(200)]
        public string? NewPassword { get; set; }

        [StringLength(200)]
        public string? ConfirmPassword { get; set; }

        public bool IsActive { get; set; } = true;

        public List<Guid> RoleIds { get; set; } = [];
    }

    public sealed class PasswordInput
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "Kata sandi baru wajib diisi.")]
        [StringLength(200)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Ulangi kata sandi baru.")]
        [StringLength(200)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    // ------------------------------------------------------------- Simpan akun

    public async Task<IActionResult> OnPostSaveAsync(AccountInput input, CancellationToken cancellationToken)
    {
        Input = input;

        Input.Username = Input.Username?.Trim() ?? string.Empty;
        Input.FullName = Input.FullName?.Trim() ?? string.Empty;
        Input.Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim();

        var isNew = Input.Id is null || Input.Id == Guid.Empty;

        var duplicate = await _context.AdminUsers
            .AsNoTracking()
            .AnyAsync(x => x.Id != (Input.Id ?? Guid.Empty)
                        && x.Username.ToLower() == Input.Username.ToLower(), cancellationToken);

        if (duplicate)
        {
            ModelState.AddModelError("Input.Username", "Nama pengguna ini sudah dipakai.");
        }

        if (isNew)
        {
            ValidatePassword(Input.NewPassword, Input.ConfirmPassword, "Input.NewPassword", "Input.ConfirmPassword");
        }

        if (Input.EmployeeId.HasValue &&
            !await _context.Employees.AnyAsync(e => e.Id == Input.EmployeeId.Value && e.DeletedAt == null, cancellationToken))
        {
            ModelState.AddModelError("Input.EmployeeId", "Pegawai yang dipilih tidak ditemukan.");
        }

        // Akun yang sedang dipakai tidak boleh menonaktifkan dirinya sendiri; kalau
        // dibiarkan, pengelola bisa terkunci di luar sistem tanpa jalan masuk lain.
        if (!isNew && Input.Id == CurrentUserId && !Input.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Anda tidak dapat menonaktifkan akun yang sedang Anda pakai.");
        }

        if (!ModelState.IsValid)
        {
            ReopenDialogName = "accountDialog";
            await LoadAsync(cancellationToken);
            return Page();
        }

        var now = DateTime.UtcNow;
        var validRoleIds = await ValidRoleIdsAsync(Input.RoleIds, cancellationToken);

        if (isNew)
        {
            var entity = new AdminUser
            {
                Id = Guid.NewGuid(),
                Username = Input.Username,
                PasswordHash = PasswordHasher.Hash(Input.NewPassword!),
                FullName = Input.FullName,
                Email = Input.Email,
                EmployeeId = Input.EmployeeId,
                IsActive = Input.IsActive,
                CreatedBy = CurrentUserName,
                CreatedAt = now
            };

            _context.AdminUsers.Add(entity);

            foreach (var roleId in validRoleIds)
            {
                _context.AdminUserRoles.Add(new AdminUserRole
                {
                    AdminUserId = entity.Id,
                    RoleId = roleId,
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Akun \"{entity.Username}\" berhasil dibuat.");
        }
        else
        {
            var entity = await _context.AdminUsers
                .Include(x => x.AdminUserRoles)
                .FirstOrDefaultAsync(x => x.Id == Input.Id, cancellationToken);

            if (entity is null)
            {
                NotifyError("Akun tidak ditemukan.");
                return RedirectToList();
            }

            if (!Input.IsActive && await IsLastActiveSuperAdminAsync(entity.Id, cancellationToken))
            {
                NotifyError("Akun ini satu-satunya Super Administrator yang aktif, jadi tidak boleh dinonaktifkan.");
                return RedirectToList();
            }

            entity.Username = Input.Username;
            entity.FullName = Input.FullName;
            entity.Email = Input.Email;
            entity.EmployeeId = Input.EmployeeId;
            entity.IsActive = Input.IsActive;
            entity.UpdatedBy = CurrentUserName;
            entity.UpdatedAt = now;

            SyncRoles(entity, validRoleIds, now);

            await _context.SaveChangesAsync(cancellationToken);
            Notify($"Akun \"{entity.Username}\" berhasil diperbarui.");
        }

        return RedirectToList();
    }

    // -------------------------------------------------------- Ganti kata sandi

    public async Task<IActionResult> OnPostResetPasswordAsync(PasswordInput password, CancellationToken cancellationToken)
    {
        Password = password;

        ValidatePassword(Password.NewPassword, Password.ConfirmPassword, "Password.NewPassword", "Password.ConfirmPassword");

        if (!ModelState.IsValid)
        {
            ReopenDialogName = "passwordDialog";
            await LoadAsync(cancellationToken);
            return Page();
        }

        var entity = await _context.AdminUsers.FirstOrDefaultAsync(x => x.Id == Password.Id, cancellationToken);

        if (entity is null)
        {
            NotifyError("Akun tidak ditemukan.");
            return RedirectToList();
        }

        entity.PasswordHash = PasswordHasher.Hash(Password.NewPassword);

        // Mengganti kata sandi sekaligus membuka kunci: jika akunnya terkunci karena
        // salah sandi berulang kali, sandi baru memang dimaksudkan untuk dipakai.
        entity.FailedLoginCount = 0;
        entity.LockedUntil = null;
        entity.UpdatedBy = CurrentUserName;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        Notify($"Kata sandi akun \"{entity.Username}\" berhasil diganti.");

        return RedirectToList();
    }

    // ------------------------------------------------------------- Buka kunci

    public async Task<IActionResult> OnPostUnlockAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.AdminUsers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            NotifyError("Akun tidak ditemukan.");
            return RedirectToList();
        }

        entity.FailedLoginCount = 0;
        entity.LockedUntil = null;
        entity.UpdatedBy = CurrentUserName;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        Notify($"Kunci akun \"{entity.Username}\" berhasil dibuka.");

        return RedirectToList();
    }

    // ------------------------------------------------------------------ Hapus

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (id == CurrentUserId)
        {
            NotifyError("Anda tidak dapat menghapus akun yang sedang Anda pakai.");
            return RedirectToList();
        }

        var entity = await _context.AdminUsers
            .Include(x => x.AdminUserRoles)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
        {
            NotifyError("Akun tidak ditemukan.");
            return RedirectToList();
        }

        if (await IsLastActiveSuperAdminAsync(entity.Id, cancellationToken))
        {
            NotifyError("Akun ini satu-satunya Super Administrator yang aktif, jadi tidak boleh dihapus.");
            return RedirectToList();
        }

        _context.AdminUserRoles.RemoveRange(entity.AdminUserRoles);
        _context.AdminUsers.Remove(entity);

        await _context.SaveChangesAsync(cancellationToken);
        Notify($"Akun \"{entity.Username}\" berhasil dihapus.");

        return RedirectToList();
    }

    // ---------------------------------------------------------------- Bantuan

    private void ValidatePassword(string? password, string? confirmation, string passwordKey, string confirmKey)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(passwordKey, "Kata sandi wajib diisi.");
            return;
        }

        if (password.Length < MinPasswordLength)
        {
            ModelState.AddModelError(passwordKey, $"Kata sandi minimal {MinPasswordLength} karakter.");
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            ModelState.AddModelError(passwordKey, "Kata sandi harus memuat huruf dan angka.");
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            ModelState.AddModelError(confirmKey, "Ulangan kata sandi tidak sama.");
        }
    }

    /// <summary>Menyaring id peran kiriman terhadap peran yang benar-benar ada dan aktif.</summary>
    private async Task<List<Guid>> ValidRoleIdsAsync(IEnumerable<Guid> requested, CancellationToken cancellationToken)
    {
        var wanted = requested.Distinct().ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        return await _context.Roles
            .AsNoTracking()
            .Where(r => wanted.Contains(r.Id) && r.IsActive)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    private void SyncRoles(AdminUser entity, List<Guid> roleIds, DateTime now)
    {
        var existing = entity.AdminUserRoles.ToList();

        var removed = existing.Where(x => !roleIds.Contains(x.RoleId)).ToList();
        if (removed.Count > 0)
        {
            _context.AdminUserRoles.RemoveRange(removed);
        }

        var currentIds = existing.Select(x => x.RoleId).ToHashSet();

        foreach (var roleId in roleIds.Where(id => !currentIds.Contains(id)))
        {
            _context.AdminUserRoles.Add(new AdminUserRole
            {
                AdminUserId = entity.Id,
                RoleId = roleId,
                CreatedAt = now
            });
        }
    }

    /// <summary>
    /// Benar bila akun ini satu-satunya pemegang peran SUPERADMIN yang masih aktif.
    /// Dipakai agar sistem tidak pernah kehilangan pemilik terakhirnya.
    /// </summary>
    private async Task<bool> IsLastActiveSuperAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        var isSuperAdmin = await _context.AdminUserRoles
            .AsNoTracking()
            .AnyAsync(x => x.AdminUserId == userId
                        && x.Role != null
                        && x.Role.Code == AppRoles.SuperAdmin, cancellationToken);

        if (!isSuperAdmin)
        {
            return false;
        }

        var otherActive = await _context.AdminUserRoles
            .AsNoTracking()
            .CountAsync(x => x.AdminUserId != userId
                          && x.Role != null
                          && x.Role.Code == AppRoles.SuperAdmin
                          && x.AdminUser != null
                          && x.AdminUser.IsActive, cancellationToken);

        return otherActive == 0;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Roles = await _context.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleOption(r.Id, r.Code, r.Name, r.IsActive))
            .ToListAsync(cancellationToken);

        Employees = await _context.Employees
            .AsNoTracking()
            .Where(e => e.DeletedAt == null && e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new EmployeeOption(e.Id, e.FullName, e.Nip))
            .ToListAsync(cancellationToken);

        var query = _context.AdminUsers.AsNoTracking();

        TotalUnfiltered = await query.CountAsync(cancellationToken);

        var keyword = NormalizedSearch();

        if (keyword is not null)
        {
            var pattern = ContainsPattern(keyword);

            query = query.Where(x =>
                EF.Functions.ILike(x.Username, pattern, LikeEscape) ||
                EF.Functions.ILike(x.FullName, pattern, LikeEscape) ||
                (x.Email != null && EF.Functions.ILike(x.Email, pattern, LikeEscape)));
        }

        TotalItems = await query.CountAsync(cancellationToken);
        ClampPage();

        // Peran diambil sebagai koleksi, bukan digabung jadi teks di dalam kueri:
        // string.Join tidak punya padanan SQL dan akan gagal diterjemahkan.
        var rows = await query
            .OrderBy(x => x.Username)
            .Skip(RowOffset)
            .Take(PageSize)
            .Select(x => new
            {
                x.Id,
                x.Username,
                x.FullName,
                x.Email,
                x.EmployeeId,
                EmployeeName = x.Employee != null ? x.Employee.FullName : null,
                x.IsActive,
                x.LastLoginAt,
                x.LockedUntil,
                RoleNames = x.AdminUserRoles.Where(r => r.Role != null).Select(r => r.Role!.Name).ToList(),
                RoleIds = x.AdminUserRoles.Select(r => r.RoleId).ToList()
            })
            .ToListAsync(cancellationToken);

        Items = rows
            .Select(x => new Row(
                x.Id, x.Username, x.FullName, x.Email, x.EmployeeId, x.EmployeeName, x.IsActive,
                x.LastLoginAt, x.LockedUntil, x.RoleNames, x.RoleIds))
            .ToList();
    }
}
