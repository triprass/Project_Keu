namespace Project_Keu.Infrastructure.Notifications;

/// <summary>
/// Pengaturan pengiriman pemberitahuan. Nilai rahasia (kunci API) tidak disimpan di
/// appsettings.json melainkan lewat environment variable, sama seperti connection string.
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// Saklar utama. Sengaja mati secara bawaan supaya pemasangan baru tidak langsung
    /// mengirim pesan ke nomor sungguhan sebelum pengaturannya sengaja dinyalakan.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Kode negara untuk nomor yang ditulis dalam bentuk lokal ("08xx"). Nomor yang
    /// sudah diawali "+" dianggap sudah lengkap dan tidak diutak-atik.
    /// </summary>
    public string DefaultCountryCode { get; set; } = "62";

    /// <summary>
    /// Nomor penerima tambahan untuk pemberitahuan pertanyaan baru, mis. nomor grup
    /// piket. Digabung dengan nomor pengelola yang berhak menjawab, bukan menggantinya.
    /// </summary>
    public string[] AdminRecipients { get; set; } = [];

    /// <summary>Alamat aplikasi yang disertakan di akhir pesan; kosongkan bila tidak perlu.</summary>
    public string? PortalUrl { get; set; }

    public WahaOptions Waha { get; set; } = new();

    /// <summary>Benar bila WAHA sudah cukup dikonfigurasi untuk dipakai mengirim.</summary>
    public bool IsWahaConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(Waha.BaseUrl) &&
        Uri.TryCreate(Waha.BaseUrl, UriKind.Absolute, out _);

    public sealed class WahaOptions
    {
        /// <summary>Alamat dasar peladen WAHA, mis. <c>http://localhost:3000</c>.</summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>Nama sesi WhatsApp pada WAHA; satu peladen bisa memuat beberapa sesi.</summary>
        public string Session { get; set; } = "default";

        /// <summary>Nilai header <c>X-Api-Key</c>. Isi lewat environment variable.</summary>
        public string? ApiKey { get; set; }

        public int TimeoutSeconds { get; set; } = 20;
    }
}
