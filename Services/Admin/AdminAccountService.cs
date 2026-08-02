using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Infrastructure;
using Project_Keu.Models;

namespace Project_Keu.Services.Admin;

/// <summary>
/// Autentikasi akun administrator terhadap tabel <c>tb_m_admin_user</c>, lengkap
/// dengan pemuatan peran dan izin miliknya.
/// </summary>
public sealed class AdminAccountService
{
    /// <summary>Jumlah kegagalan berturut-turut sebelum akun dikunci sementara.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>Lama penguncian setelah batas kegagalan tercapai.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _context;
    private readonly ILogger<AdminAccountService> _logger;

    public AdminAccountService(AppDbContext context, ILogger<AdminAccountService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public enum AuthOutcome
    {
        Success,
        InvalidCredential,
        Disabled,
        LockedOut
    }

    public sealed record AuthResult(
        AuthOutcome Outcome,
        Guid UserId,
        string Username,
        string FullName,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions,
        DateTime? LockedUntil)
    {
        public bool Succeeded => Outcome == AuthOutcome.Success;

        public static AuthResult Failed(AuthOutcome outcome, DateTime? lockedUntil = null) =>
            new(outcome, Guid.Empty, string.Empty, string.Empty, [], [], lockedUntil);
    }

    /// <summary>Belum ada satu pun akun admin aktif, sehingga login belum bisa dipakai.</summary>
    public Task<bool> HasAnyActiveAccountAsync(CancellationToken cancellationToken = default)
    {
        return _context.AdminUsers.AsNoTracking().AnyAsync(x => x.IsActive, cancellationToken);
    }

    public async Task<AuthResult> AuthenticateAsync(string? username, string? password, CancellationToken cancellationToken = default)
    {
        var normalized = (username ?? string.Empty).Trim().ToLowerInvariant();

        if (normalized.Length == 0 || string.IsNullOrEmpty(password))
        {
            return AuthResult.Failed(AuthOutcome.InvalidCredential);
        }

        // Entitas dilacak (tanpa AsNoTracking) karena hitungan kegagalan dan
        // waktu login terakhir ikut diperbarui di bawah.
        var user = await _context.AdminUsers
            .FirstOrDefaultAsync(x => x.Username.ToLower() == normalized, cancellationToken);

        if (user is null)
        {
            // Tetap membakar waktu setara satu verifikasi, supaya lama respons tidak
            // membedakan "nama pengguna tidak ada" dari "kata sandi salah".
            PasswordHasher.PerformDummyWork();
            return AuthResult.Failed(AuthOutcome.InvalidCredential);
        }

        if (!user.IsActive)
        {
            PasswordHasher.PerformDummyWork();
            return AuthResult.Failed(AuthOutcome.Disabled);
        }

        var now = DateTime.UtcNow;

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > now)
        {
            PasswordHasher.PerformDummyWork();
            return AuthResult.Failed(AuthOutcome.LockedOut, user.LockedUntil);
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            user.FailedLoginCount++;

            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntil = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;

                _logger.LogWarning(
                    "Akun admin '{Username}' dikunci sampai {LockedUntil:u} setelah {Attempts} kegagalan.",
                    user.Username, user.LockedUntil, MaxFailedAttempts);
            }

            user.UpdatedAt = now;
            await _context.SaveChangesAsync(cancellationToken);

            return user.LockedUntil.HasValue && user.LockedUntil.Value > now
                ? AuthResult.Failed(AuthOutcome.LockedOut, user.LockedUntil)
                : AuthResult.Failed(AuthOutcome.InvalidCredential);
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        user.UpdatedAt = now;
        await _context.SaveChangesAsync(cancellationToken);

        var (roles, permissions) = await LoadAccessAsync(user.Id, cancellationToken);

        return new AuthResult(
            AuthOutcome.Success,
            user.Id,
            user.Username,
            string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName,
            roles,
            permissions,
            null);
    }

    /// <summary>Kode peran dan kode izin efektif milik satu akun.</summary>
    private async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> LoadAccessAsync(
        Guid adminUserId,
        CancellationToken cancellationToken)
    {
        var activeRoles = _context.AdminUserRoles
            .AsNoTracking()
            .Where(x => x.AdminUserId == adminUserId && x.Role != null && x.Role.IsActive);

        var roles = await activeRoles
            .Select(x => x.Role!.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        var permissions = await activeRoles
            .SelectMany(x => x.Role!.RolePermissions)
            .Where(rp => rp.Permission != null && rp.Permission.IsActive)
            .Select(rp => rp.Permission!.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        return (roles, permissions);
    }
}
