using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;

namespace Project_Keu.Services.Questions;

public sealed class QuestionGroupsService
{
    private readonly AppDbContext _context;

    public QuestionGroupsService(AppDbContext context)
    {
        _context = context;
    }

    public sealed class QuestionGroupDto
    {
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public List<QuestionItemDto> Questions { get; set; } = new();
    }

    public sealed class QuestionItemDto
    {
        public Guid Id { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public async Task<List<QuestionGroupDto>> GetQuestionGroupsAsync()
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
            .Select(g => new QuestionGroupDto
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                Questions = g
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new QuestionItemDto
                    {
                        Id = x.Id,
                        QuestionText = x.QuestionText,
                        CreatedAt = x.CreatedAt
                    })
                    .ToList()
            })
            .ToListAsync();

        return groups;
    }
}
