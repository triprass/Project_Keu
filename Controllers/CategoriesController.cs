using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _context.Categories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
            return NotFound(new { message = "Category not found" });

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Category request)
    {
        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
        request.CreatedAt = request.CreatedAt == default ? DateTime.UtcNow : request.CreatedAt;

        _context.Categories.Add(request);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Category request)
    {
        var item = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound(new { message = "Category not found" });

        item.Code = request.Code;
        item.Name = request.Name;
        item.Description = request.Description;
        item.IsActive = request.IsActive;
        item.UpdatedBy = request.UpdatedBy;
        item.UpdatedAt = request.UpdatedAt ?? DateTime.UtcNow;
        item.DeletedBy = request.DeletedBy;
        item.DeletedAt = request.DeletedAt;

        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.Categories.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound(new { message = "Category not found" });

        _context.Categories.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
