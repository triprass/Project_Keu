using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

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

    public async Task OnGetAsync()
    {
        await LoadQuestionGroupsAsync();
    }

    private async Task LoadQuestionGroupsAsync()
    {
        var groups = await _context.Questions
            .AsNoTracking()
            .Include(q => q.Category)
            .OrderByDescending(q => q.CreatedAt)
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
                    .Select(x => new QuestionItemViewModel
                    {
                        Id = x.Id,
                        QuestionText = x.QuestionText,
                        CreatedAt = x.CreatedAt
                    })
                    .ToList()
            })
            .ToListAsync();

        QuestionGroups = groups;
    }
}
