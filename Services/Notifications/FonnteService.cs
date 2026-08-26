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
            // Mengambil Token dari appsettings.json
            _token = configuration["Fonnte:Token"] ?? throw new ArgumentNullException("Token Fonnte belum dikonfigurasi.");
        }

        public async Task<string> SendWhatsAppMessageAsync(string target, string message, string countryCode = "62")
        {
            var requestUrl = "https://api.fonnte.com/send";

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            // Menambahkan Token di Header Authorization tanpa skema (sesuai spesifikasi Fonnte)
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
    }
}