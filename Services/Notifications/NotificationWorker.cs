using Microsoft.Extensions.Options;
using Project_Keu.Infrastructure.Notifications;

namespace Project_Keu.Services.Notifications;

/// <summary>
/// Menjalankan pekerjaan pemberitahuan di luar request. Setiap pekerjaan mendapat
/// lingkup layanan sendiri karena DbContext bersifat scoped dan tidak boleh dipakai
/// bersama antar pekerjaan.
/// </summary>
public sealed class NotificationWorker : BackgroundService
{
    private readonly NotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationWorker> _logger;

    public NotificationWorker(
        NotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationOptions> options,
        ILogger<NotificationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Pemberitahuan dimatikan (Notifications:Enabled = false).");
            return;
        }

        if (!_options.IsWahaConfigured)
        {
            _logger.LogWarning(
                "Pemberitahuan dinyalakan tetapi Notifications:Waha:BaseUrl belum diisi dengan alamat yang sah. " +
                "Pesan tidak akan terkirim.");
        }

        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<NotificationDispatcher>();

                await dispatcher.HandleAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Galat satu pekerjaan tidak boleh menghentikan pekerja; kalau ini
                // dilempar, seluruh pemberitahuan berikutnya ikut mati diam-diam.
                _logger.LogError(ex,
                    "Gagal memproses pemberitahuan {Kind} untuk pertanyaan {QuestionId}.",
                    job.Kind, job.QuestionId);
            }
        }
    }
}
