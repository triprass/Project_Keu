using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Project_Keu.Infrastructure;
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

    // Mengembalikan seluruh kolom data pegawai (email, telepon, tanggal lahir),
    // jadi tidak boleh terbuka untuk publik.
    [HttpGet]
    [RequireApiKey]
    public async Task<IActionResult> GetEmployees()
    {
        var employees = await _service.GetAllAsync();

        return Ok(employees);
    }

    [HttpGet("{id:guid}")]
    [RequireApiKey]
    public async Task<IActionResult> GetEmployeeById(Guid id)
    {
        var employee = await _service.GetByIdAsync(id);

        if (employee is null)
        {
            return NotFound(new { message = "Employee not found" });
        }

        return Ok(employee);
    }

    // Dipakai form pertanyaan untuk mengisi nama dari NIP, jadi tetap anonim,
    // tetapi dibatasi rate limit agar tidak bisa dipakai menyapu data pegawai.
    [HttpGet("by-nip/{nip}")]
    [EnableRateLimiting("employee-lookup")]
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
