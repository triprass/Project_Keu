using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Keu.Models;

/// <summary>Relasi banyak-ke-banyak antara akun admin dan peran.</summary>
[Table("tb_r_admin_user_role")]
public class AdminUserRole
{
    [Column("admin_user_id")]
    public Guid AdminUserId { get; set; }

    [Column("role_id")]
    public Guid RoleId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public AdminUser? AdminUser { get; set; }
    public Role? Role { get; set; }
}
