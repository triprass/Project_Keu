using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Services.AdminDashboardV2;

public sealed class AdminDashboardV2QueryService
{
    private readonly AppDbContext _context;

    public AdminDashboardV2QueryService(AppDbContext context)
    {
        _context = context;
    }

    public sealed class QueryRequest
    {
        public string? Q { get; set; }
        public string? EmployeeKeyword { get; set; }
        public string? CategoryKeyword { get; set; }
        public string? StatusKeyword { get; set; }
        public string? QuestionKeyword { get; set; }
        public DateTime? CreatedDate { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? StatusId { get; set; }
        public Guid? CreatedByEmployee { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public sealed class QueryResult
    {
        public List<Question> Questions { get; set; } = new();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public async Task<QueryResult> GetQuestionsAsync(QueryRequest request)
    {
        if (request.Page < 1) request.Page = 1;
        if (request.PageSize <= 0) request.PageSize = 10;
        if (request.PageSize > 100) request.PageSize = 100;

        var query = _context.Questions
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Status)
            .Include(x => x.CreatedByEmployeeNavigation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var search = request.Q.Trim();
            query = query.Where(x =>
                (x.QuestionNo != null && x.QuestionNo.Contains(search)) ||
                (x.Title != null && x.Title.Contains(search)) ||
                (x.QuestionText != null && x.QuestionText.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.EmployeeKeyword))
        {
            var keyword = request.EmployeeKeyword.Trim();
            query = query.Where(x =>
                (x.CreatedByEmployeeNavigation != null && x.CreatedByEmployeeNavigation.FullName.Contains(keyword)) ||
                (x.CreatedByEmployeeNavigation != null && x.CreatedByEmployeeNavigation.Nip != null && x.CreatedByEmployeeNavigation.Nip.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(request.CategoryKeyword))
        {
            var keyword = request.CategoryKeyword.Trim();
            query = query.Where(x => x.Category != null && x.Category.Name.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.StatusKeyword))
        {
            var keyword = request.StatusKeyword.Trim();
            query = query.Where(x => x.Status != null && x.Status.Name.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.QuestionKeyword))
        {
            var keyword = request.QuestionKeyword.Trim();
            query = query.Where(x =>
                (x.QuestionText != null && x.QuestionText.Contains(keyword)) ||
                (x.Title != null && x.Title.Contains(keyword)) ||
                (x.QuestionNo != null && x.QuestionNo.Contains(keyword)));
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == request.CategoryId.Value);
        }

        if (request.StatusId.HasValue)
        {
            query = query.Where(x => x.StatusId == request.StatusId.Value);
        }

        if (request.CreatedByEmployee.HasValue)
        {
            query = query.Where(x => x.CreatedByEmployee == request.CreatedByEmployee.Value);
        }

        if (request.CreatedDate.HasValue)
        {
            var date = request.CreatedDate.Value.Date;
            var next = date.AddDays(1);
            query = query.Where(x => x.CreatedAt >= date && x.CreatedAt < next);
        }

        if (request.DateFrom.HasValue)
        {
            var fromDate = request.DateFrom.Value.Date;
            query = query.Where(x => x.CreatedAt >= fromDate);
        }

        if (request.DateTo.HasValue)
        {
            var toDateExclusive = request.DateTo.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < toDateExclusive);
        }

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)request.PageSize));

        if (request.Page > totalPages) request.Page = totalPages;

        var questions = await query
            .OrderByDescending(x => x.CreatedAt)
            //.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        return new QueryResult
        {
            Questions = questions,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}


public class AdminDashboardV2Model : PageModel
{
    private readonly AppDbContext _context;
    private readonly AdminDashboardV2QueryService _queryService;

    public AdminDashboardV2Model(AppDbContext context, AdminDashboardV2QueryService queryService)
    {
        _context = context;
        _queryService = queryService;
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

    public sealed class AdminDashboardV2ApiResult
    {
        public List<Question>? Questions { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
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
