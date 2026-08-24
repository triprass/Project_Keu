using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using System.Collections.Concurrent;

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

    // Kata kunci pencarian yang diambil dari query string (?q=...)
    public string? SearchQuery { get; private set; }

    /// <summary>Jumlah pertanyaan yang ditampilkan per kartu kategori sebelum "Tampilkan lebih banyak".</summary>
    private const int PreviewPerCategory = 5;

    public Task OnGetAsync(string? q, CancellationToken cancellationToken)
    {
        SearchQuery = string.IsNullOrWhiteSpace(q) ? null : q;
        return LoadQuestionGroupsAsync(SearchQuery, cancellationToken);
    }

    private async Task LoadQuestionGroupsAsync(string? searchQuery, CancellationToken cancellationToken)
    {
        // Hanya beberapa pertanyaan terbaru per kategori yang dirender di kartu;
        // sebelumnya seluruh isi tabel ditarik ke memori setiap halaman dibuka.
        var query = _context.Questions
            .AsNoTracking()
            .Include(x => x.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            // Filter pertanyaan berdasarkan teks pertanyaan atau nama kategori (case-insensitive)
            var pattern = $"%{searchQuery}%";
            query = query.Where(x => EF.Functions.Like(x.QuestionText, pattern)
                                      || (x.Category != null && EF.Functions.Like(x.Category.Name, pattern)));
        }

        QuestionGroups = await query
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