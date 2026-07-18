using Microsoft.AspNetCore.Mvc;
using Project_Keu.Services.Employees;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly EmployeeService _service;

    public EmployeesController(EmployeeService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetEmployees()
    {
        var employees = await _service.GetAllAsync();

        return Ok(employees);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEmployeeById(Guid id)
    {
        var employee = await _service.GetByIdAsync(id);

        if (employee is null)
        {
            return NotFound(new { message = "Employee not found" });
        }

        return Ok(employee);
    }

    [HttpGet("by-nip/{nip}")]
    public async Task<IActionResult> GetEmployeeByNip(string nip)
    {
        if (string.IsNullOrWhiteSpace(nip))
        {
            return BadRequest(new { message = "NIP is required" });
        }

        var employee = await _service.GetByNipSummaryAsync(nip);

        if (employee is null)
        {
            return NotFound(new { message = "Employee not found" });
        }

        return Ok(employee);
    }
}
