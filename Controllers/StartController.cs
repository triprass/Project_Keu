using Microsoft.AspNetCore.Mvc;
using Project_Keu.Services.Questions;

namespace Project_Keu.Controllers;

[Route("start")]
public sealed class StartController : Controller
{
    private readonly QuestionsLandingService _landingService;

    public StartController(QuestionsLandingService landingService)
    {
        _landingService = landingService;
    }

    // controller -> service -> db (via service) -> service -> controller
    // then redirect to Razor Page for render
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        await _landingService.PrepareQuestionGroupsAsync();
        return RedirectToPage("/Pertanyaan");
    }
}
