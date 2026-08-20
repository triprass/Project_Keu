using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Project_Keu.Infrastructure.Notifications;

namespace Project_Keu.Services.Notifications;

/// <summary>
/// Antrean pemberitahuan antara request dan pekerja latar belakang.
///
/// Pengiriman tidak dilakukan di dalam request karena WAHA bisa lambat atau mati, dan
/// pegawai tidak boleh menunggu (apalagi gagal menyimpan pertanyaan) hanya karena
/// pemberitahuannya tidak terkirim.
/// </summary>
public sealed class NotificationQueue
{
    /// <summary>
    /// Antrean dibatasi supaya lonjakan tidak menghabiskan memori. Saat penuh, pekerjaan
    /// terbaru dibuang dan dicatat - menahan request demi sebuah pemberitahuan justru
    /// menghukum pengguna atas masalah yang bukan miliknya.
    /// </summary>
    private const int Capacity = 500;

    private readonly Channel<NotificationJob> _channel;
    private readonly NotificationOptions _options;
    private readonly ILogger<NotificationQueue> _logger;

    public NotificationQueue(IOptions<NotificationOptions> options, ILogger<NotificationQueue> logger)
    {
        _options = options.Value;
        _logger = logger;

        _channel = Channel.CreateBounded<NotificationJob>(new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>Menitipkan pekerjaan. Tidak pernah melempar galat maupun menunggu.</summary>
    public void Enqueue(NotificationJob job)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (!_channel.Writer.TryWrite(job))
        {
            _logger.LogWarning(
                "Antrean pemberitahuan penuh, {Kind} untuk pertanyaan {QuestionId} dilewati.",
                job.Kind, job.QuestionId);
        }
    }

    public IAsyncEnumerable<NotificationJob> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
