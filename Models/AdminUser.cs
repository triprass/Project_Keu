using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Keu.Models;

/// <summary>Akun pengelola aplikasi. Terpisah dari <see cref="Employee"/> supaya data kepegawaian tidak bercampur dengan kredensial.</summary>
[Table("tb_m_admin_user")]
public class AdminUser
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>Format "PBKDF2-SHA512$iterasi$salt$hash". Kata sandi asli tidak pernah disimpan.</summary>
    [Required]
    [MaxLength(255)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(150)]
    [Column("email")]
    public string? Email { get; set; }

    /// <summary>Pegawai yang terkait, bila akun ini milik pegawai tertentu.</summary>
    [Column("employee_id")]
    public Guid? EmployeeId { get; set; }

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Jumlah kegagalan login berturut-turut; dipakai untuk penguncian sementara.</summary>
    [Required]
    [Column("failed_login_count")]
    public int FailedLoginCount { get; set; }

    /// <summary>Akun tidak bisa dipakai masuk sampai waktu ini terlewati.</summary>
    [Column("locked_until")]
    public DateTime? LockedUntil { get; set; }

    [MaxLength(100)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [MaxLength(100)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    public ICollection<AdminUserRole> AdminUserRoles { get; set; } = new List<AdminUserRole>();
}
