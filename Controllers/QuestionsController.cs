using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/questions")]
public class QuestionsController : ControllerBase
{
    private readonly AppDbContext _context;

    public QuestionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _context.Questions
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Status)
            .Include(x => x.CreatedByEmployeeNavigation)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _context.Questions
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Status)
            .Include(x => x.CreatedByEmployeeNavigation)
            .Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
            return NotFound(new { message = "Question not found" });

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Question request)
    {
        var categoryExists = await _context.QuestionCategories.AnyAsync(x => x.Id == request.CategoryId);
        if (!categoryExists) return BadRequest(new { message = "Invalid category_id" });

        var statusExists = await _context.QuestionStatuses.AnyAsync(x => x.Id == request.StatusId);
        if (!statusExists) return BadRequest(new { message = "Invalid status_id" });

        var employeeExists = await _context.Employees.AnyAsync(x => x.Id == request.CreatedByEmployee);
        if (!employeeExists) return BadRequest(new { message = "Invalid created_by_employee" });

        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
        request.CreatedAt = request.CreatedAt == default ? DateTime.UtcNow : request.CreatedAt;

        _context.Questions.Add(request);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Question request)
    {
        var item = await _context.Questions.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound(new { message = "Question not found" });

        var categoryExists = await _context.QuestionCategories.AnyAsync(x => x.Id == request.CategoryId);
        if (!categoryExists) return BadRequest(new { message = "Invalid category_id" });

        var statusExists = await _context.QuestionStatuses.AnyAsync(x => x.Id == request.StatusId);
        if (!statusExists) return BadRequest(new { message = "Invalid status_id" });

        var employeeExists = await _context.Employees.AnyAsync(x => x.Id == request.CreatedByEmployee);
        if (!employeeExists) return BadRequest(new { message = "Invalid created_by_employee" });

        item.QuestionNo = request.QuestionNo;
        item.CategoryId = request.CategoryId;
        item.Title = request.Title;
        item.QuestionText = request.QuestionText;
        item.CreatedByEmployee = request.CreatedByEmployee;
        item.StatusId = request.StatusId;
        item.UpdatedAt = request.UpdatedAt ?? DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.Questions.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound(new { message = "Question not found" });

        _context.Questions.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
