using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Keu.Models;

/// <summary>Relasi banyak-ke-banyak antara peran dan izin.</summary>
[Table("tb_r_role_permission")]
public class RolePermission
{
    [Column("role_id")]
    public Guid RoleId { get; set; }

    [Column("permission_id")]
    public Guid PermissionId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}
