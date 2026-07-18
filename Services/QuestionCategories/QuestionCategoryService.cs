using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Services.QuestionCategories;

public sealed class QuestionCategoryService
{
    private readonly AppDbContext _context;

    public QuestionCategoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<QuestionCategory>> GetAllAsync()
    {
        return await _context.QuestionCategories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<QuestionCategory?> GetByIdAsync(Guid id)
    {
        return await _context.QuestionCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<QuestionCategory> CreateAsync(QuestionCategory request)
    {
        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
        request.CreatedAt = request.CreatedAt == default ? DateTime.UtcNow : request.CreatedAt;

        _context.QuestionCategories.Add(request);
        await _context.SaveChangesAsync();

        return request;
    }

    public async Task<QuestionCategory?> UpdateAsync(Guid id, QuestionCategory request)
    {
        var item = await _context.QuestionCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return null;

        item.Code = request.Code;
        item.Name = request.Name;
        item.Description = request.Description;
        item.IsActive = request.IsActive;
        item.UpdatedBy = request.UpdatedBy;
        item.UpdatedAt = request.UpdatedAt ?? DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var item = await _context.QuestionCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return false;

        _context.QuestionCategories.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }
}
