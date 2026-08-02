using Microsoft.AspNetCore.Mvc;
using Project_Keu.Services.AdminDashboardV2;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/admin-dashboard-v2")]
public class AdminDashboardV2Controller : ControllerBase
{
    private readonly AdminDashboardV2QueryService _service;
    private readonly AdminDashboardV2ExportService _exportService;

    public AdminDashboardV2Controller(
        AdminDashboardV2QueryService service,
        AdminDashboardV2ExportService exportService)
    {
        _service = service;
        _exportService = exportService;
    }

    [HttpGet("questions")]
    public async Task<IActionResult> GetQuestions(
        [FromQuery] string? q,
        [FromQuery] string? employeeKeyword,
        [FromQuery] string? categoryKeyword,
        [FromQuery] string? statusKeyword,
        [FromQuery] string? questionKeyword,
        [FromQuery] DateTime? createdDate,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? statusId,
        [FromQuery] Guid? createdByEmployee,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var request = new AdminDashboardV2QueryService.QueryRequest
        {
            Q = q,
            EmployeeKeyword = employeeKeyword,
            CategoryKeyword = categoryKeyword,
            StatusKeyword = statusKeyword,
            QuestionKeyword = questionKeyword,
            CreatedDate = createdDate,
            CategoryId = categoryId,
            StatusId = statusId,
            CreatedByEmployee = createdByEmployee,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Page = page,
            PageSize = pageSize
        };

        var result = await _service.GetQuestionsAsync(request);
        return Ok(result);
    }

    [HttpGet("questions/export")]
    public async Task<IActionResult> ExportQuestions(
        [FromQuery] string? q,
        [FromQuery] string? employeeKeyword,
        [FromQuery] string? categoryKeyword,
        [FromQuery] string? statusKeyword,
        [FromQuery] string? questionKeyword,
        [FromQuery] DateTime? createdDate,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? statusId,
        [FromQuery] Guid? createdByEmployee,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo)
    {
        var request = new AdminDashboardV2QueryService.QueryRequest
        {
            Q = q,
            EmployeeKeyword = employeeKeyword,
            CategoryKeyword = categoryKeyword,
            StatusKeyword = statusKeyword,
            QuestionKeyword = questionKeyword,
            CreatedDate = createdDate,
            CategoryId = categoryId,
            StatusId = statusId,
            CreatedByEmployee = createdByEmployee,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Page = 1,
            PageSize = 10000
        };

        var result = await _service.GetQuestionsAsync(request);
        var fileBytes = _exportService.BuildExcelCompatibleCsv(result.Questions);

        var fileName = $"admin-dashboard-v2-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(fileBytes, "text/csv; charset=utf-8", fileName);
    }
}
