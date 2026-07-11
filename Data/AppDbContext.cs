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
    }
}
