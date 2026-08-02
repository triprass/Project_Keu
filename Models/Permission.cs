using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Keu.Models;

/// <summary>Izin satuan. Kodenya yang dipakai pada <c>[Authorize(Policy = "questions.export")]</c>.</summary>
[Table("tb_m_permission")]
public class Permission
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>Kode izin bergaya "grup.aksi", mis. "questions.export".</summary>
    [Required]
    [MaxLength(100)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Pengelompokan untuk tampilan layar pengaturan hak akses.</summary>
    [MaxLength(50)]
    [Column("group_name")]
    public string? GroupName { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
