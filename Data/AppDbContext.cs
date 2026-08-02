using Microsoft.EntityFrameworkCore;
using Project_Keu.Models;

namespace Project_Keu.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<QuestionCategory> QuestionCategories => Set<QuestionCategory>();
    public DbSet<QuestionStatus> QuestionStatuses => Set<QuestionStatus>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Answer> Answers => Set<Answer>();

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<AdminUserRole> AdminUserRoles => Set<AdminUserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasOne(q => q.Category)
                .WithMany(c => c.Questions)
                .HasForeignKey(q => q.CategoryId);

            entity.HasOne(q => q.CreatedByEmployeeNavigation)
                .WithMany()
                .HasForeignKey(q => q.CreatedByEmployee);

            entity.HasOne(q => q.Status)
                .WithMany(s => s.Questions)
                .HasForeignKey(q => q.StatusId);
        });

        modelBuilder.Entity<Answer>(entity =>
        {
            entity.HasOne(a => a.Question)
                .WithMany(q => q.Answers)
                .HasForeignKey(a => a.QuestionId);

            entity.HasOne(a => a.AnsweredByEmployee)
                .WithMany()
                .HasForeignKey(a => a.AnsweredBy);
        });

        ConfigureAccessControl(modelBuilder);
    }

    /// <summary>
    /// Pemetaan tabel hak akses (akun admin - peran - izin). Tabel fisiknya dibuat
    /// lewat skrip <c>Database/001_admin_rbac.sql</c>, bukan oleh EF Migrations,
    /// karena skema basis data proyek ini dikelola di luar aplikasi.
    /// </summary>
    private static void ConfigureAccessControl(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasIndex(x => x.Username).IsUnique();

            entity.HasOne(x => x.Employee)
                .WithMany()
                .HasForeignKey(x => x.EmployeeId);
        });

        modelBuilder.Entity<Role>(entity => entity.HasIndex(x => x.Code).IsUnique());

        modelBuilder.Entity<Permission>(entity => entity.HasIndex(x => x.Code).IsUnique());

        modelBuilder.Entity<AdminUserRole>(entity =>
        {
            entity.HasKey(x => new { x.AdminUserId, x.RoleId });

            entity.HasOne(x => x.AdminUser)
                .WithMany(u => u.AdminUserRoles)
                .HasForeignKey(x => x.AdminUserId);

            entity.HasOne(x => x.Role)
                .WithMany(r => r.AdminUserRoles)
                .HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(x => new { x.RoleId, x.PermissionId });

            entity.HasOne(x => x.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(x => x.RoleId);

            entity.HasOne(x => x.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(x => x.PermissionId);
        });
    }
}
