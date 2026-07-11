using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Pages;

public class FormPertanyaanModel : PageModel
{
    private readonly AppDbContext _context;
    private static readonly Guid DefaultStatusId = Guid.Parse("589362d4-83e4-457f-af89-dad137b68845");

    public FormPertanyaanModel(AppDbContext context)
    {
        _context = context;
    }

    public List<Category> Categories { get; private set; } = new();

    [BindProperty]
    public string? Pertanyaan { get; set; }

    [BindProperty]
    public Guid? CategoryId { get; set; }

    [BindProperty]
    public string? Nip { get; set; }

    [BindProperty]
    public string? Nama { get; set; }

    [BindProperty]
    public Guid? EmployeeId { get; set; }

    public async Task OnGetAsync()
    {
        await LoadCategoriesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCategoriesAsync();

        if (string.IsNullOrWhiteSpace(Pertanyaan) || CategoryId is null || EmployeeId is null)
        {
            ModelState.AddModelError(string.Empty, "Data belum lengkap. Mohon isi pertanyaan, kategori, dan NIP yang valid.");
            return Page();
        }

        var category = await _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == CategoryId.Value && c.IsActive && c.DeletedAt == null);

        if (category is null)
        {
            ModelState.AddModelError(string.Empty, "Kategori tidak valid.");
            return Page();
        }

        var employeeExists = await _context.Employees
            .AsNoTracking()
            .AnyAsync(e => e.Id == EmployeeId.Value);

        if (!employeeExists)
        {
            ModelState.AddModelError(string.Empty, "Pegawai tidak valid.");
            return Page();
        }

        var questionNo = await GenerateQuestionNoAsync();

        var question = new Question
        {
            Id = Guid.NewGuid(),
            QuestionNo = questionNo,
            CategoryId = CategoryId.Value,
            Title = category.Name,
            QuestionText = Pertanyaan.Trim(),
            CreatedByEmployee = EmployeeId.Value,
            StatusId = DefaultStatusId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Questions.Add(question);
        await _context.SaveChangesAsync();

        return RedirectToPage("/DetailPertanyaan");
    }

    private async Task LoadCategoriesAsync()
    {
        Categories = await _context.Categories
            .AsNoTracking()
            .Where(x => x.IsActive && x.DeletedAt == null)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    private async Task<string> GenerateQuestionNoAsync()
    {
        var now = DateTime.UtcNow;
        var prefix = $"Q{now:yyyyMM}";
        var maxExisting = await _context.Questions
            .AsNoTracking()
            .Where(q => q.QuestionNo != null && q.QuestionNo.StartsWith(prefix))
            .Select(q => q.QuestionNo!)
            .ToListAsync();

        var maxRunning = 0;
        foreach (var qn in maxExisting)
        {
            if (qn.Length >= prefix.Length + 3)
            {
                var suffix = qn.Substring(prefix.Length, 3);
                if (int.TryParse(suffix, out var num) && num > maxRunning)
                {
                    maxRunning = num;
                }
            }
        }

        return $"{prefix}{(maxRunning + 1):D3}";
    }
}
