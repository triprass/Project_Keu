using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;

namespace Project_Keu.Pages;

public class PertanyaanModel : PageModel
{
    private readonly AppDbContext _context;

    public PertanyaanModel(AppDbContext context)
    {
        _context = context;
    }

    public sealed class QuestionGroupViewModel
    {
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public List<QuestionItemViewModel> Questions { get; set; } = new();
    }

    public sealed class QuestionItemViewModel
    {
        public Guid Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public List<QuestionGroupViewModel> QuestionGroups { get; private set; } = new();

    /// <summary>Jumlah pertanyaan yang ditampilkan per kartu kategori sebelum "Tampilkan lebih banyak".</summary>
    private const int PreviewPerCategory = 5;

    public Task OnGetAsync(CancellationToken cancellationToken)
    {
        return LoadQuestionGroupsAsync(cancellationToken);
    }

    private async Task LoadQuestionGroupsAsync(CancellationToken cancellationToken)
    {
        // Hanya beberapa pertanyaan terbaru per kategori yang dirender di kartu;
        // sebelumnya seluruh isi tabel ditarik ke memori setiap halaman dibuka.
        QuestionGroups = await _context.Questions
            .AsNoTracking()
            .GroupBy(q => new
            {
                q.CategoryId,
                CategoryName = q.Category != null ? q.Category.Name : "-"
            })
            .Select(g => new QuestionGroupViewModel
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                Questions = g
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(PreviewPerCategory)
                    .Select(x => new QuestionItemViewModel
                    {
                        Id = x.Id,
                        QuestionText = x.QuestionText,
                        CreatedAt = x.CreatedAt
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }
}
