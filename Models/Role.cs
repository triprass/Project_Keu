using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Keu.Models;

/// <summary>Peran, yaitu sekumpulan izin yang diberikan bersama-sama kepada akun admin.</summary>
[Table("tb_m_role")]
public class Role
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Kode tetap yang dipakai kode program, mis. "SUPERADMIN".</summary>
    [Required]
    [MaxLength(50)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public ICollection<AdminUserRole> AdminUserRoles { get; set; } = new List<AdminUserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
