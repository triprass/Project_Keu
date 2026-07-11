using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/question-statuses")]
public class QuestionStatusesController : ControllerBase
{
    private readonly AppDbContext _context;

    public QuestionStatusesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _context.QuestionStatuses
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _context.QuestionStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
            return NotFound(new { message = "Question status not found" });

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] QuestionStatus request)
    {
        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;

        _context.QuestionStatuses.Add(request);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] QuestionStatus request)
    {
        var item = await _context.QuestionStatuses.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound(new { message = "Question status not found" });

        item.Code = request.Code;
        item.Name = request.Name;
        item.Color = request.Color;
        item.IsActive = request.IsActive;

        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.QuestionStatuses.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound(new { message = "Question status not found" });

        _context.QuestionStatuses.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
