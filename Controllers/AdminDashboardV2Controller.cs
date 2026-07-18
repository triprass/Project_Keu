using Microsoft.AspNetCore.Mvc;
using Project_Keu.Services.AdminDashboardV2;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/admin-dashboard-v2")]
public class AdminDashboardV2Controller : ControllerBase
{
    private readonly AdminDashboardV2QueryService _service;

    public AdminDashboardV2Controller(AdminDashboardV2QueryService service)
    {
        _service = service;
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
}
