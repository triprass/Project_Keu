using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Services.QuestionStatuses;

public sealed class QuestionStatusService
{
    private readonly AppDbContext _context;

    public QuestionStatusService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<QuestionStatus>> GetAllAsync()
    {
        return await _context.QuestionStatuses
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<QuestionStatus?> GetByIdAsync(Guid id)
    {
        return await _context.QuestionStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<QuestionStatus> CreateAsync(QuestionStatus request)
    {
        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;

        _context.QuestionStatuses.Add(request);
        await _context.SaveChangesAsync();

        return request;
    }

    public async Task<QuestionStatus?> UpdateAsync(Guid id, QuestionStatus request)
    {
        var item = await _context.QuestionStatuses.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return null;

        item.Code = request.Code;
        item.Name = request.Name;
        item.Color = request.Color;
        item.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var item = await _context.QuestionStatuses.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return false;

        _context.QuestionStatuses.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }
}
