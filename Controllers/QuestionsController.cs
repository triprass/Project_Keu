using Microsoft.AspNetCore.Mvc;
using Project_Keu.Models;
using Project_Keu.Services.Questions;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/questions")]
public class QuestionsController : ControllerBase
{
    private readonly QuestionService _service;

    public QuestionsController(QuestionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _service.GetAllAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _service.GetByIdAsync(id);

        if (item is null)
            return NotFound(new { message = "Question not found" });

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Question request)
    {
        var result = await _service.CreateAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Question request)
    {
        var result = await _service.UpdateAsync(id, request);

        if (!result.Success && result.ErrorMessage == "Question not found")
            return NotFound(new { message = "Question not found" });

        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { message = "Question not found" });

        return NoContent();
    }
}
