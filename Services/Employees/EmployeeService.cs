using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Services.Employees;

public sealed class EmployeeService
{
    private readonly AppDbContext _context;

    public EmployeeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Employee>> GetAllAsync()
    {
        return await _context.Employees
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .ToListAsync();
    }

    public async Task<Employee?> GetByIdAsync(Guid id)
    {
        return await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<object?> GetByNipSummaryAsync(string nip)
    {
        return await _context.Employees
            .AsNoTracking()
            .Where(e => e.Nip != null && e.Nip == nip)
            .Select(e => new
            {
                e.Id,
                e.Nip,
                FullName = e.FullName
            })
            .FirstOrDefaultAsync();
    }
}
