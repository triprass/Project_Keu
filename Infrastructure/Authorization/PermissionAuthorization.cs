using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Project_Keu.Infrastructure.Authorization;

/// <summary>Nama tipe claim yang menyimpan kode izin pada cookie autentikasi.</summary>
public static class AppClaimTypes
{
    public const string Permission = "permission";
}

/// <summary>Peran bawaan yang selalu dianggap memiliki seluruh izin.</summary>
public static class AppRoles
{
    public const string SuperAdmin = "SUPERADMIN";
}

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;

    public string Permission { get; }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // SUPERADMIN lolos tanpa perlu didaftarkan satu per satu, supaya izin baru
        // yang ditambahkan nanti tidak mengunci pemilik sistem.
        if (context.User.IsInRole(AppRoles.SuperAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var hasPermission = context.User.Claims.Any(c =>
            c.Type == AppClaimTypes.Permission &&
            string.Equals(c.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Membentuk policy otorisasi secara otomatis dari nama policy yang dipakai, sehingga
/// <c>[Authorize(Policy = "questions.export")]</c> langsung berfungsi tanpa perlu
/// mendaftarkan tiap izin satu per satu di Program.cs. Izin baru cukup ditambahkan
/// di tabel <c>tb_m_permission</c> lalu dipasang pada peran.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    /// <summary>Awalan opsional; "perm:questions.export" maupun "questions.export" sama-sama diterima.</summary>
    public const string Prefix = "perm:";

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Policy yang didaftarkan manual tetap diutamakan.
        var declared = await _fallback.GetPolicyAsync(policyName);

        if (declared is not null)
        {
            return declared;
        }

        var permission = policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            ? policyName[Prefix.Length..]
            : policyName;

        // Hanya nama bergaya "grup.aksi" yang diperlakukan sebagai izin, agar salah
        // ketik nama policy tidak diam-diam berubah menjadi izin yang tidak pernah ada.
        if (!permission.Contains('.'))
        {
            return null;
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission))
            .Build();
    }
}
