using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Project_Keu.Services.Notifications
{
    public class WablasService : IWablasService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WablasService> _logger;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly int _delayMs;

        public WablasService(HttpClient httpClient, IConfiguration configuration, ILogger<WablasService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiKey = configuration["Wablas:ApiKey"] ?? throw new ArgumentNullException("API Key Wablas belum dikonfigurasi.");
            _baseUrl = configuration["Wablas:BaseUrl"] ?? "https://rhino.wablas.com";
            _delayMs = int.TryParse(configuration["Wablas:DelayMs"], out int delay) ? delay : 5000; // Default delay 5 detik
        }

        public async Task<string> SendWhatsAppMessageAsync(string target, string message)
        {
            // 1. Eksekusi Delay sebelum pengiriman HTTP Request
            if (_delayMs > 0)
            {
                await Task.Delay(_delayMs);
            }

            var requestUrl = $"{_baseUrl.TrimEnd('/')}/api/v2/send-message";
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            // Header Authorization untuk Wablas
            request.Headers.TryAddWithoutValidation("Authorization", _apiKey);

            // Format JSON Payload sesuai dokumentasi API Wablas v2
            var payload = new
            {
                data = new[]
                {
                    new
                    {
                        phone = target,
                        message = message
                    }
                }
            };

            string jsonContent = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string responseString = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Wablas API Response: {Response}", responseString);
                return responseString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gagal mengirim pesan WhatsApp via Wablas ke {Target}", target);
                throw;
            }
        }

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