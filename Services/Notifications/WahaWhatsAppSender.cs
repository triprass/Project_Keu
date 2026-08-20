using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Project_Keu.Infrastructure.Notifications;

namespace Project_Keu.Services.Notifications;

/// <summary>
/// Pengirim WhatsApp lewat WAHA (https://waha.devlike.pro): <c>POST /api/sendText</c>
/// dengan header <c>X-Api-Key</c>.
/// </summary>
public sealed class WahaWhatsAppSender : IWhatsAppSender
{
    private const string SendTextPath = "api/sendText";

    /// <summary>Batas potongan badan balasan yang ikut dicatat saat gagal, supaya log tidak membengkak.</summary>
    private const int MaxLoggedResponseLength = 500;

    private readonly HttpClient _httpClient;
    private readonly NotificationOptions _options;
    private readonly ILogger<WahaWhatsAppSender> _logger;

    public WahaWhatsAppSender(
        HttpClient httpClient,
        IOptions<NotificationOptions> options,
        ILogger<WahaWhatsAppSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendTextAsync(string chatId, string text, CancellationToken cancellationToken)
    {
        if (!_options.IsWahaConfigured)
        {
            _logger.LogWarning("Pemberitahuan WhatsApp dilewati: WAHA belum dikonfigurasi.");
            return false;
        }

        var payload = new SendTextRequest(_options.Waha.Session, chatId, text);

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(SendTextPath, payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Pemberitahuan WhatsApp terkirim ke {ChatId}.", chatId);
                return true;
            }

            var body = await ReadTruncatedAsync(response, cancellationToken);

            _logger.LogError(
                "WAHA menolak pengiriman ke {ChatId}. Status {StatusCode}. Balasan: {Body}",
                chatId, (int)response.StatusCode, body);

            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Aplikasi sedang berhenti; bukan kegagalan yang perlu dicatat sebagai galat.
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Gagal menghubungi WAHA saat mengirim pemberitahuan ke {ChatId}.", chatId);
            return false;
        }
    }

    private static async Task<string> ReadTruncatedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return body.Length > MaxLoggedResponseLength
                ? body[..MaxLoggedResponseLength] + "..."
                : body;
        }
        catch (Exception)
        {
            return "(tidak terbaca)";
        }
    }

    /// <summary>Bentuk badan permintaan WAHA; nama propertinya harus persis seperti ini.</summary>
    private sealed record SendTextRequest(
        [property: JsonPropertyName("session")] string Session,
        [property: JsonPropertyName("chatId")] string ChatId,
        [property: JsonPropertyName("text")] string Text);
}
