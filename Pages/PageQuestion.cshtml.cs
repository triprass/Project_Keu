using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Services.PageQuestion;

namespace Project_Keu.Pages;

public class PageQuestionModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly PageQuestionQueryService _queryService;

    public PageQuestionModel(AppDbContext context, PageQuestionQueryService queryService)
    {
        _context = context;
        _queryService = queryService;
    }

    public List<PageQuestionQueryService.QuestionResponse> Questions { get; private set; } = new();

    public List<SelectListItem> StatusOptions { get; private set; } = new();

    // Semua filter di-bind dari form body (POST), bukan query string,
    // supaya CategoryId dan parameter lain tidak terlihat di URL.
    [BindProperty]
    public string? Q { get; set; }

    [BindProperty]
    public string? EmployeeKeyword { get; set; }

    [BindProperty]
    public string? CategoryKeyword { get; set; }

    [BindProperty]
    public string? StatusKeyword { get; set; }

    [BindProperty]
    public string? QuestionKeyword { get; set; }

    [BindProperty]
    public DateTime? CreatedDate { get; set; }

    [BindProperty]
    public Guid? CategoryId { get; set; }

    [BindProperty]
    public Guid? StatusId { get; set; }

    [BindProperty]
    public Guid? CreatedByEmployee { get; set; }

    [BindProperty]
    public DateTime? DateFrom { get; set; }

    [BindProperty]
    public DateTime? DateTo { get; set; }

    // Dinamai PageNumber agar tidak menutupi method PageModel.Page();
    // nama field pada form tetap "Page".
    [BindProperty(Name = "Page")]
    public int PageNumber { get; set; } = 1;

    [BindProperty]
    public int PageSize { get; set; } = 10;

    public int TotalItems { get; private set; }
    public int TotalPages { get; private set; }

    public Task OnGetAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    public Task OnPostAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await LoadFilterOptionsAsync(cancellationToken);

        var result = await _queryService.GetQuestionsAsync(new PageQuestionQueryService.QueryRequest
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
            Page = PageNumber,
            PageSize = PageSize
        }, cancellationToken);

        Questions = result.Questions;
        TotalItems = result.TotalItems;
        TotalPages = result.TotalPages;
        PageNumber = result.Page;
        PageSize = result.PageSize;
    }

    private async Task LoadFilterOptionsAsync(CancellationToken cancellationToken)
    {
        // Hanya daftar status yang benar-benar dirender pada halaman. Query kategori
        // dan seluruh tabel pegawai sebelumnya dijalankan setiap request tanpa dipakai.
        StatusOptions = await _context.QuestionStatuses
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name
            })
            .ToListAsync(cancellationToken);
    }
}
