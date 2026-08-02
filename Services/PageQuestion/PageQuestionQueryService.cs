using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;

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

    public sealed class QuestionResponse
    {
        public Guid Id { get; set; }
        public string QuestionNo { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string QuestionText { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public sealed class QueryResult
    {
        public List<QuestionResponse> Questions { get; set; } = new();
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
                x.CreatedByEmployeeNavigation != null &&
                (
                    (x.CreatedByEmployeeNavigation.FullName != null &&
                     EF.Functions.Like(x.CreatedByEmployeeNavigation.FullName, $"%{keyword}%")) ||
                    (x.CreatedByEmployeeNavigation.Nip != null &&
                     EF.Functions.Like(x.CreatedByEmployeeNavigation.Nip, $"%{keyword}%"))
                ));
        }

        if (!string.IsNullOrWhiteSpace(request.CategoryKeyword))
        {
            var keyword = request.CategoryKeyword.Trim();
            query = query.Where(x =>
                x.Category != null &&
                x.Category.Name != null &&
                x.Category.Name.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(request.StatusKeyword))
        {
            var keyword = request.StatusKeyword.Trim();
            query = query.Where(x =>
                x.Status != null &&
                x.Status.Name != null &&
                x.Status.Name.Contains(keyword));
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
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new QuestionResponse
            {
                Id = x.Id,
                QuestionNo = x.QuestionNo ?? string.Empty,
                Title = x.Title ?? string.Empty,
                QuestionText = x.QuestionText ?? string.Empty,
                CategoryName = x.Category != null ? (x.Category.Name ?? string.Empty) : string.Empty,
                StatusName = x.Status != null ? (x.Status.Name ?? string.Empty) : string.Empty,
                EmployeeName = x.CreatedByEmployeeNavigation != null ? (x.CreatedByEmployeeNavigation.FullName ?? string.Empty) : string.Empty,
                CreatedAt = x.CreatedAt
            })
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
