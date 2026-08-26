using Microsoft.AspNetCore.Mvc;
using Project_Keu.Infrastructure;
using Project_Keu.Models;
using Project_Keu.Services.Notifications;
using Project_Keu.Services.Questions;
using SixLabors.Fonts;

namespace Project_Keu.Controllers;

// Endpoint administratif: mengembalikan data pegawai lengkap dan mengizinkan
// perubahan/penghapusan data, sehingga seluruhnya butuh kunci API.
[ApiController]
[Route("api/questions")]
[RequireApiKey]
public class QuestionsController : ControllerBase
{
    private readonly QuestionService _service;
    private readonly IFonnteService _fonnteService; // Inject FonnteService

    public QuestionsController(QuestionService service, IFonnteService fonnteService)
    {
        _service = service;
        _fonnteService = fonnteService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var items = await _service.GetAllAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var item = await _service.GetByIdAsync(id);

        if (item is null)
            return NotFound(new { message = "Question not found" });

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Question request)
    {
        var result = await _service.CreateAsync(request);
        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Question request)
    {
        var result = await _service.UpdateAsync(id, request);

        if (!result.Success && result.ErrorMessage == "Question not found")
            return NotFound(new { message = "Question not found" });

        if (!result.Success)
            return BadRequest(new { message = result.ErrorMessage });

        return Ok(result.Data);
    }


    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _service.DeleteAsync(id);
        if (!deleted)
            return NotFound(new { message = "Question not found" });

        // --- Panggilan Notifikasi Fonnte ---
        string targetPhone = "082298157376"; // Ganti dengan nomor tujuan (bisa ambil dari DB/User)
        string message = $"Halo pesan ini dari Fonnte";

        // Jalankan pengiriman WA
        await _fonnteService.SendWhatsAppMessageAsync(targetPhone, message);

        return NoContent();
    }
}
