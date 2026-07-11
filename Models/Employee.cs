using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Keu.Models;

[Table("tb_m_employee")]
public class Employee
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("employee_no")]
    public string EmployeeNo { get; set; } = string.Empty;

    [MaxLength(30)]
    [Column("nip")]
    public string? Nip { get; set; }

    [Required]
    [MaxLength(150)]
    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("nick_name")]
    public string? NickName { get; set; }

    [MaxLength(150)]
    [Column("email")]
    public string? Email { get; set; }

    [MaxLength(30)]
    [Column("phone_number")]
    public string? PhoneNumber { get; set; }

    [Column("gender", TypeName = "char(1)")]
    public string? Gender { get; set; }

    [Column("birth_date")]
    public DateOnly? BirthDate { get; set; }

    [Column("division_id")]
    public Guid? DivisionId { get; set; }

    [Column("department_id")]
    public Guid? DepartmentId { get; set; }

    [Column("position_id")]
    public Guid? PositionId { get; set; }

    [Column("manager_id")]
    public Guid? ManagerId { get; set; }

    [MaxLength(100)]
    [Column("company")]
    public string? Company { get; set; }

    [MaxLength(100)]
    [Column("branch")]
    public string? Branch { get; set; }

    [MaxLength(100)]
    [Column("location")]
    public string? Location { get; set; }

    [Column("hire_date")]
    public DateOnly? HireDate { get; set; }

    [Column("resign_date")]
    public DateOnly? ResignDate { get; set; }

    [MaxLength(30)]
    [Column("employment_status")]
    public string? EmploymentStatus { get; set; }

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; }

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

    [MaxLength(100)]
    [Column("deleted_by")]
    public string? DeletedBy { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }
}
