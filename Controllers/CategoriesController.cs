using Microsoft.AspNetCore.Mvc;
using Project_Keu.Models;
using Project_Keu.Services.Categories;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly CategoryService _service;

    public CategoriesController(CategoryService service)
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
            return NotFound(new { message = "Category not found" });

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Category request)
    {
        var item = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Category request)
    {
        var item = await _service.UpdateAsync(id, request);
        if (item is null)
            return NotFound(new { message = "Category not found" });

        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { message = "Category not found" });

        return NoContent();
    }
}
