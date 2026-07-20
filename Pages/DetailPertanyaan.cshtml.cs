using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Project_Keu.Data;
using Project_Keu.Models;

namespace Project_Keu.Pages;

public class DetailPertanyaanModel : PageModel
{
    private readonly AppDbContext _context;

    public DetailPertanyaanModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? Id { get; set; }

    public Question? SelectedQuestion { get; private set; }

    public string AnswerTextDisplay { get; private set; } = "Belum ada respon dari PIC Keuangan";

    public bool IsClosed { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Id.HasValue || Id.Value == Guid.Empty)
        {
            return Page();
        }

        SelectedQuestion = await _context.Questions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == Id.Value);

        if (SelectedQuestion is null)
        {
            return Page();
        }

        var latestAnswer = await _context.Answers
            .AsNoTracking()
            .Where(a => a.QuestionId == SelectedQuestion.Id)
            .OrderByDescending(a => a.AnsweredAt ?? DateTime.MinValue)
            .FirstOrDefaultAsync();

        if (latestAnswer is not null && !string.IsNullOrWhiteSpace(latestAnswer.AnswerText))
        {
            AnswerTextDisplay = latestAnswer.AnswerText;
            IsClosed = true;
        }

        return Page();
    }
}
