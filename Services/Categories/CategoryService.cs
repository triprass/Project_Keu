using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Services.Categories;

public sealed class CategoryService
{
    private readonly AppDbContext _context;

    public CategoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Category>> GetAllAsync()
    {
        return await _context.Categories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<Category?> GetByIdAsync(Guid id)
    {
        return await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Category> CreateAsync(Category request)
    {
        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
        request.CreatedAt = request.CreatedAt == default ? DateTime.UtcNow : request.CreatedAt;

        _context.Categories.Add(request);
        await _context.SaveChangesAsync();

        return request;
    }

    public async Task<Category?> UpdateAsync(Guid id, Category request)
    {
        var item = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return null;

        item.Code = request.Code;
        item.Name = request.Name;
        item.Description = request.Description;
        item.IsActive = request.IsActive;
        item.UpdatedBy = request.UpdatedBy;
        item.UpdatedAt = request.UpdatedAt ?? DateTime.UtcNow;
        item.DeletedBy = request.DeletedBy;
        item.DeletedAt = request.DeletedAt;

        await _context.SaveChangesAsync();
        return item;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var item = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return false;

        _context.Categories.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }
}
