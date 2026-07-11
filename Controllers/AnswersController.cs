using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/answers")]
public class AnswersController : ControllerBase
{
    private readonly AppDbContext _context;

    public AnswersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery(Name = "question_id")] Guid? questionId = null)
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

        var items = await query
            .OrderByDescending(x => x.AnsweredAt)
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _context.Answers
            .AsNoTracking()
            .Include(x => x.Question)
            .Include(x => x.AnsweredByEmployee)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (item is null)
            return NotFound(new { message = "Answer not found" });

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Answer request)
    {
        var questionExists = await _context.Questions.AnyAsync(x => x.Id == request.QuestionId);
        if (!questionExists) return BadRequest(new { message = "Invalid question_id" });

        var employeeExists = await _context.Employees.AnyAsync(x => x.Id == request.AnsweredBy);
        if (!employeeExists) return BadRequest(new { message = "Invalid answered_by" });

        request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
        request.AnsweredAt ??= DateTime.UtcNow;

        _context.Answers.Add(request);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Answer request)
    {
        var item = await _context.Answers.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound(new { message = "Answer not found" });

        var questionExists = await _context.Questions.AnyAsync(x => x.Id == request.QuestionId);
        if (!questionExists) return BadRequest(new { message = "Invalid question_id" });

        var employeeExists = await _context.Employees.AnyAsync(x => x.Id == request.AnsweredBy);
        if (!employeeExists) return BadRequest(new { message = "Invalid answered_by" });

        item.QuestionId = request.QuestionId;
        item.AnswerText = request.AnswerText;
        item.AnsweredBy = request.AnsweredBy;
        item.AnsweredAt = request.AnsweredAt ?? item.AnsweredAt ?? DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await _context.Answers.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null)
            return NotFound(new { message = "Answer not found" });

        _context.Answers.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
