using Microsoft.AspNetCore.Mvc;
using Project_Keu.Models;
using Project_Keu.Services.Answers;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/answers")]
public class AnswersController : ControllerBase
{
    private readonly AnswerService _service;

    public AnswersController(AnswerService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery(Name = "question_id")] Guid? questionId = null)
    {
        var items = await _service.GetAllAsync(questionId);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _service.GetByIdAsync(id);

        if (item is null)
            return NotFound(new { message = "Answer not found" });

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Answer request)
    {
        var result = await _service.CreateAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Answer request)
    {
        var result = await _service.UpdateAsync(id, request);

        if (!result.Success && result.ErrorMessage == "Answer not found")
            return NotFound(new { message = "Answer not found" });

        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Data);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { message = "Answer not found" });

        return NoContent();
    }
}
