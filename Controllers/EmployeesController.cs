using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmployeesController : ControllerBase
{
    private readonly AppDbContext _context;

    public EmployeesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetEmployees()
    {
        var employees = await _context.Employees
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .ToListAsync();

        return Ok(employees);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEmployeeById(Guid id)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

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

        var employee = await _context.Employees
            .AsNoTracking()
            .Where(e => e.Nip != null && e.Nip == nip)
            .Select(e => new
            {
                e.Id,
                e.Nip,
                FullName = e.FullName
            })
            .FirstOrDefaultAsync();

        if (employee is null)
        {
            return NotFound(new { message = "Employee not found" });
        }

        return Ok(employee);
    }
}
