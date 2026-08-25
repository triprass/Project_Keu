namespace Project_Keu.Services.Notifications
{
    public interface IAzureAcsService
    {
        // 1. Template: jawab_pertanyaan (Untuk Notifikasi ke PIC Keuangan)
        Task<bool> SendJawabPertanyaanAsync(string picPhone, string noTiket, string namaPengaju, string unitKerja);

        // 2. Template: pertanyaan_telah_ditindaklanjuti (Untuk Pengaju)
        Task<bool> SendPertanyaanTelahDitindaklanjutiAsync(string recipientPhone, string namaPengaju, string noTiket);

        // 3. Template: pertanyaan_berhasil_dibuat (Untuk Pengaju)
        Task<bool> SendPertanyaanBerhasilDibuatAsync(string recipientPhone, string namaPengaju, string noTiket);
    }
}