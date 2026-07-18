using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Services.Answers;

public sealed class AnswerService
{
    private readonly AppDbContext _context;

    public AnswerService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Answer>> GetAllAsync(Guid? questionId = null)
    {
        var query = _context.Answers
            .AsNoTracking()
            .Include(x => x.Question)
            .Include(x => x.AnsweredByEmployee)
            .AsQueryable();

        if (questionId.HasValue)
        {
            query = query.Where(x => x.QuestionId == questionId.Value);
        }

        return await query
            .OrderByDescending(x => x.AnsweredAt)
            .ToListAsync();
    }

    public async Task<Answer?> GetByIdAsync(Guid id)
    {
        return await _context.Answers
            .AsNoTracking()
            .Include(x => x.Question)
            .Include(x => x.AnsweredByEmployee)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<(bool Success, string? ErrorMessage, Answer? Data)> CreateAsync(Answer request)
    {
        var questionExists = await _context.Questions.AnyAsync(x => x.Id == request.QuestionId);
        if (!questionExists) return (false, "Invalid question_id", null);

        var employeeExists = await _context.Employees.AnyAsync(x => x.Id == request.AnsweredBy);
        if (!employeeExists) return (false, "Invalid answered_by", null);

        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
        request.AnsweredAt ??= DateTime.UtcNow;

        _context.Answers.Add(request);
        await _context.SaveChangesAsync();

        return (true, null, request);
    }

    public async Task<(bool Success, string? ErrorMessage, Answer? Data)> UpdateAsync(Guid id, Answer request)
    {
        var item = await _context.Answers.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return (false, "Answer not found", null);

        var questionExists = await _context.Questions.AnyAsync(x => x.Id == request.QuestionId);
        if (!questionExists) return (false, "Invalid question_id", null);

        var employeeExists = await _context.Employees.AnyAsync(x => x.Id == request.AnsweredBy);
        if (!employeeExists) return (false, "Invalid answered_by", null);

        item.QuestionId = request.QuestionId;
        item.AnswerText = request.AnswerText;
        item.AnsweredBy = request.AnsweredBy;
        item.AnsweredAt = request.AnsweredAt ?? item.AnsweredAt ?? DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return (true, null, item);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var item = await _context.Answers.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return false;

        _context.Answers.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }
}
