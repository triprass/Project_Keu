namespace Project_Keu.Services.Notifications;

/// <summary>
/// Pengirim satu pesan teks WhatsApp. Dipisah sebagai antarmuka supaya penyedia
/// layanannya bisa diganti tanpa menyentuh penyusun isi pesan.
/// </summary>
public interface IWhatsAppSender
{
    /// <summary>
    /// Mengirim pesan. Mengembalikan false bila gagal; kegagalan pemberitahuan tidak
    /// boleh membatalkan pekerjaan yang memicunya, jadi tidak dilempar sebagai galat.
    /// </summary>
    Task<bool> SendTextAsync(string chatId, string text, CancellationToken cancellationToken);
}
