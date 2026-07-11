using Microsoft.EntityFrameworkCore;
using Project_Keu.Models;

namespace Project_Keu.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();
}
