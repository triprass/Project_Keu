using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project_Keu.Models;

[Table("tb_t_answer")]
public class Answer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("question_id")]
    public Guid QuestionId { get; set; }

    [Required]
    [Column("answer")]
    public string AnswerText { get; set; } = string.Empty;

    [Required]
    [Column("answered_by")]
    public Guid AnsweredBy { get; set; }

    [Column("answered_at")]
    public DateTime? AnsweredAt { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public Question? Question { get; set; }

    [ForeignKey(nameof(AnsweredBy))]
    public Employee? AnsweredByEmployee { get; set; }
}
