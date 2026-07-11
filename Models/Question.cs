using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Keu.Models;

[Table("tb_t_question")]
public class Question
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [MaxLength(30)]
    [Column("question_no")]
    public string? QuestionNo { get; set; }

    [Required]
    [Column("category_id")]
    public Guid CategoryId { get; set; }

    [Required]
    [MaxLength(500)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column("question")]
    public string QuestionText { get; set; } = string.Empty;

    [Required]
    [Column("created_by_employee")]
    public Guid CreatedByEmployee { get; set; }

    [Required]
    [Column("status_id")]
    public Guid StatusId { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey(nameof(CategoryId))]
    public QuestionCategory? Category { get; set; }

    [ForeignKey(nameof(CreatedByEmployee))]
    public Employee? CreatedByEmployeeNavigation { get; set; }

    [ForeignKey(nameof(StatusId))]
    public QuestionStatus? Status { get; set; }

    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
}
