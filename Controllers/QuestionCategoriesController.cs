using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/question-categories")]
public class QuestionCategoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public QuestionCategoriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _context.QuestionCategories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _context.QuestionCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
            return NotFound(new { message = "Question category not found" });

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] QuestionCategory request)
    {
        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
        request.CreatedAt = request.CreatedAt == default ? DateTime.UtcNow : request.CreatedAt;

        _context.QuestionCategories.Add(request);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] QuestionCategory request)
    {
        var item = await _context.QuestionCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound(new { message = "Question category not found" });

        item.Code = request.Code;
        item.Name = request.Name;
        item.Description = request.Description;
        item.IsActive = request.IsActive;
        item.UpdatedBy = request.UpdatedBy;
        item.UpdatedAt = request.UpdatedAt ?? DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.QuestionCategories.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound(new { message = "Question category not found" });

        _context.QuestionCategories.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
