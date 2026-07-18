using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Services.Questions;

public sealed class QuestionService
{
    private readonly AppDbContext _context;

    public QuestionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Question>> GetAllAsync()
    {
        return await _context.Questions
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Status)
            .Include(x => x.CreatedByEmployeeNavigation)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<Question?> GetByIdAsync(Guid id)
    {
        return await _context.Questions
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Status)
            .Include(x => x.CreatedByEmployeeNavigation)
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<(bool Success, string? ErrorMessage, Question? Data)> CreateAsync(Question request)
    {
        var categoryExists = await _context.QuestionCategories.AnyAsync(x => x.Id == request.CategoryId);
        if (!categoryExists) return (false, "Invalid category_id", null);

        var statusExists = await _context.QuestionStatuses.AnyAsync(x => x.Id == request.StatusId);
        if (!statusExists) return (false, "Invalid status_id", null);

        var employeeExists = await _context.Employees.AnyAsync(x => x.Id == request.CreatedByEmployee);
        if (!employeeExists) return (false, "Invalid created_by_employee", null);

        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
        request.CreatedAt = request.CreatedAt == default ? DateTime.UtcNow : request.CreatedAt;

        _context.Questions.Add(request);
        await _context.SaveChangesAsync();

        return (true, null, request);
    }

    public async Task<(bool Success, string? ErrorMessage, Question? Data)> UpdateAsync(Guid id, Question request)
    {
        var item = await _context.Questions.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return (false, "Question not found", null);

        var categoryExists = await _context.QuestionCategories.AnyAsync(x => x.Id == request.CategoryId);
        if (!categoryExists) return (false, "Invalid category_id", null);

        var statusExists = await _context.QuestionStatuses.AnyAsync(x => x.Id == request.StatusId);
        if (!statusExists) return (false, "Invalid status_id", null);

        var employeeExists = await _context.Employees.AnyAsync(x => x.Id == request.CreatedByEmployee);
        if (!employeeExists) return (false, "Invalid created_by_employee", null);

        item.QuestionNo = request.QuestionNo;
        item.CategoryId = request.CategoryId;
        item.Title = request.Title;
        item.QuestionText = request.QuestionText;
        item.CreatedByEmployee = request.CreatedByEmployee;
        item.StatusId = request.StatusId;
        item.UpdatedAt = request.UpdatedAt ?? DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return (true, null, item);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var item = await _context.Questions.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return false;

        _context.Questions.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }
}
