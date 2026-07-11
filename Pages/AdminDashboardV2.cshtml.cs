using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Pages;

public class AdminDashboardV2Model : PageModel
{
    private readonly AppDbContext _context;

    public AdminDashboardV2Model(AppDbContext context)
    {
        _context = context;
    }

    public List<Question> Questions { get; private set; } = new();

    public List<SelectListItem> CategoryOptions { get; private set; } = new();
    public List<SelectListItem> StatusOptions { get; private set; } = new();
    public List<SelectListItem> EmployeeOptions { get; private set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EmployeeKeyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CategoryKeyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusKeyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? QuestionKeyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? CreatedDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? StatusId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? CreatedByEmployee { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DateTo { get; set; }

    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    public int TotalItems { get; private set; }
    public int TotalPages { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadFilterOptionsAsync();

        if (Page < 1) Page = 1;
        if (PageSize <= 0) PageSize = 10;
        if (PageSize > 100) PageSize = 100;

        var query = _context.Questions
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Status)
            .Include(x => x.CreatedByEmployeeNavigation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var search = Q.Trim();
            query = query.Where(x =>
                (x.QuestionNo != null && x.QuestionNo.Contains(search)) ||
                x.Title.Contains(search) ||
                x.QuestionText.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(EmployeeKeyword))
        {
            var keyword = EmployeeKeyword.Trim();
            query = query.Where(x =>
                (x.CreatedByEmployeeNavigation != null && x.CreatedByEmployeeNavigation.FullName.Contains(keyword)) ||
                (x.CreatedByEmployeeNavigation != null && x.CreatedByEmployeeNavigation.Nip != null && x.CreatedByEmployeeNavigation.Nip.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(CategoryKeyword))
        {
            var keyword = CategoryKeyword.Trim();
            query = query.Where(x => x.Category != null && x.Category.Name.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(StatusKeyword))
        {
            var keyword = StatusKeyword.Trim();
            query = query.Where(x => x.Status != null && x.Status.Name.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(QuestionKeyword))
        {
            var keyword = QuestionKeyword.Trim();
            query = query.Where(x =>
                x.QuestionText.Contains(keyword) ||
                x.Title.Contains(keyword) ||
                (x.QuestionNo != null && x.QuestionNo.Contains(keyword)));
        }

        if (CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == CategoryId.Value);
        }

        if (StatusId.HasValue)
        {
            query = query.Where(x => x.StatusId == StatusId.Value);
        }

        if (CreatedByEmployee.HasValue)
        {
            query = query.Where(x => x.CreatedByEmployee == CreatedByEmployee.Value);
        }

        if (CreatedDate.HasValue)
        {
            var date = CreatedDate.Value.Date;
            var next = date.AddDays(1);
            query = query.Where(x => x.CreatedAt >= date && x.CreatedAt < next);
        }

        if (DateFrom.HasValue)
        {
            var fromDate = DateFrom.Value.Date;
            query = query.Where(x => x.CreatedAt >= fromDate);
        }

        if (DateTo.HasValue)
        {
            var toDateExclusive = DateTo.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < toDateExclusive);
        }

        TotalItems = await query.CountAsync();
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));

        if (Page > TotalPages) Page = TotalPages;

        Questions = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    private async Task LoadFilterOptionsAsync()
    {
        CategoryOptions = await _context.QuestionCategories
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();

        StatusOptions = await _context.QuestionStatuses
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync();

        EmployeeOptions = await _context.Employees
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.FullName)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.FullName} ({x.Nip ?? "-"})"
            })
            .ToListAsync();
    }
}
