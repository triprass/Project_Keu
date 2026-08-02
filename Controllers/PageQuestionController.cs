using Microsoft.AspNetCore.Mvc;
using Project_Keu.Services.PageQuestion;

namespace Project_Keu.Controllers;

[ApiController]
[Route("api/page-question")]
[Produces("application/json")]
public class PageQuestionController : ControllerBase
{
    private readonly PageQuestionQueryService _service;
    private readonly PageQuestionExportService _exportService;

    public PageQuestionController(
        PageQuestionQueryService service,
        PageQuestionExportService exportService)
    {
        _service = service;
        _exportService = exportService;
    }

    // Filter dikirim lewat request body (POST) supaya CategoryId dan
    // parameter lain tidak muncul di URL / log akses.
    [HttpPost("questions")]
    [ProducesResponseType(typeof(PageQuestionQueryService.QueryResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetQuestions(
        [FromBody] PageQuestionQueryService.QueryRequest? request,
        CancellationToken cancellationToken)
    {
        request ??= new PageQuestionQueryService.QueryRequest();

        var result = await _service.GetQuestionsAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("questions/export")]
    public async Task<IActionResult> ExportQuestions(
        [FromBody] PageQuestionQueryService.QueryRequest? request,
        CancellationToken cancellationToken)
    {
        request ??= new PageQuestionQueryService.QueryRequest();

        var rows = await _service.GetQuestionsForExportAsync(request, cancellationToken);
        var fileBytes = _exportService.BuildWorkbook(rows);

        var fileName = $"daftar-pertanyaan-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return File(fileBytes, PageQuestionExportService.ContentType, fileName);
    }
}
