using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Services.AdminDashboardV2;

namespace Project_Keu.Pages;

public class AdminDashboardV2Model : PageModel
{
    private readonly AppDbContext _context;
    private readonly AdminDashboardV2QueryService _queryService;

    public AdminDashboardV2Model(AppDbContext context, AdminDashboardV2QueryService queryService)
    {
        _context = context;
        _queryService = queryService;
    }

    public List<AdminDashboardV2QueryService.QuestionResponse> Questions { get; private set; } = new();

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

        var result = await _queryService.GetQuestionsAsync(new AdminDashboardV2QueryService.QueryRequest
        {
            Q = Q,
            EmployeeKeyword = EmployeeKeyword,
            CategoryKeyword = CategoryKeyword,
            StatusKeyword = StatusKeyword,
            QuestionKeyword = QuestionKeyword,
            CreatedDate = CreatedDate,
            CategoryId = CategoryId,
            StatusId = StatusId,
            CreatedByEmployee = CreatedByEmployee,
            DateFrom = DateFrom,
            DateTo = DateTo,
            Page = Page,
            PageSize = PageSize
        });

        Questions = result.Questions;
        TotalItems = result.TotalItems;
        TotalPages = result.TotalPages;
        Page = result.Page;
        PageSize = result.PageSize;
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
