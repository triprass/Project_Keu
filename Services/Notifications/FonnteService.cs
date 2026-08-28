using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Project_Keu.Services.Notifications
{
    public class FonnteService : IFonnteService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FonnteService> _logger;
        private readonly string _token;

        public FonnteService(HttpClient httpClient, IConfiguration configuration, ILogger<FonnteService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _token = configuration["Fonnte:Token"] ?? throw new ArgumentNullException("Token Fonnte belum dikonfigurasi.");
        }

        public async Task<string> SendWhatsAppMessageAsync(string target, string message, string countryCode = "62")
        {
            var requestUrl = "https://api.fonnte.com/send";
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            request.Headers.TryAddWithoutValidation("Authorization", _token);

            var formData = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("target", target),
            new KeyValuePair<string, string>("message", message),
            new KeyValuePair<string, string>("countryCode", countryCode)
        };

            request.Content = new FormUrlEncodedContent(formData);

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string responseString = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Fonnte API Response: {Response}", responseString);
                return responseString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gagal mengirim pesan WhatsApp via Fonnte ke {Target}", target);
                throw;
            }
        }

        // Template notifikasi pesan untuk pembuat pertanyaan jika pertanyaannya telah dijawab
        // Parameter Pembuat Pertanyaan dan Nomor Tiket akan diisi secara dinamis
        public string BuildTicketTemplate1(string ticketNo, string senderName, string nip, string unitKerja, string kategoriPertanyaan)
        {
            return $"Halo\n" +
                   $"*{senderName}* \n\n" +
                   $"[Open]\n" +
                   $"Pengajuan Layanan dengan Nomor Tiket {ticketNo}\n" +
                   $"Nama : {senderName} / {nip} \n" +
                   $"Unit Kerja : {unitKerja} \n" +
                   $"Kategori Pertanyaan : {kategoriPertanyaan} \n\n" +
                   $"Telah kami terima dan akan segera kami proses. \n" +
                   $"Terima kasih😇";
        }

        public string BuildTicketTemplate2(string ticketNo, string senderName, string nip, string unitKerja, string kategoriPertanyaan)
        {
            return $"Halo\n" +
                   $"*PIC Keuangan* \n\n" +
                   $"[Open]\n" +
                   $"Pengajuan Layanan dengan Nomor Tiket {ticketNo}\n" +
                   $"Nama : {senderName} / {nip} \n" +
                   $"Unit Kerja : {unitKerja} \n" +
                   $"Kategori Pertanyaan : {kategoriPertanyaan} \n\n" +
                   $"Mohon untuk ditindaklanjuti. \n" +
                   $"Terima kasih😇";
        }

        public string BuildTicketTemplate3(string ticketNo, string senderName, string nip, string unitKerja, string kategoriPertanyaan, Guid Id)
        {
            return $"Halo\n" +
                   $"*{senderName}* \n\n" +
                   $"[Close]\n" +
                   $"Pengajuan Layanan dengan Nomor Tiket {ticketNo}\n" +
                   $"Nama : {senderName} / {nip} \n" +
                   $"Unit Kerja : {unitKerja} \n" +
                   $"Kategori Pertanyaan : {kategoriPertanyaan} \n\n" +
                   $"Silahkan klik link dibawah ini untuk melihat detail jawaban \n" +
                   $"https://pilarkeuangan.com/DetailPertanyaan?Id={Id} \n" +
                   $"Terima kasih😇";
        }
    }
}