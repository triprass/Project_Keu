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
        public string BuildTicketTemplate1(string senderName, string ticketNo)
        {
            return $"*[Pertanyaan Berhasil Dibuat]*\n\n" +
                   $"Halo *{senderName}*🙌,\n\n" +
                   $"Pertanyaan Anda telah berhasil dibuat dengan *Nomor {ticketNo}*.\n\n" +
                   $"PIC Keuangan akan segera menindaklanjuti pertanyaan Anda.\n\n" +
                   $"Terima kasih😇";
        }

        // Template notifikasi pesan untuk pembuat pertanyaan jika pertanyaannya telah dijawab
        // Parameter Pembuat Pertanyaan dan Nomor Tiket akan diisi secara dinamis
        //public string BuildTicketTemplate2(string senderName, string ticketNo)
        //{
        //    return $"*[Pertanyaan Telah Dijawab]*\n\n" +
        //           $"Halo *{senderName}* 🙌,\n\n" +
        //           $"Pertanyaan Anda dengan *Nomor {ticketNo}* telah selesai ditindaklanjuti oleh:\n\n" +
        //           $"Terima kasih telah menggunakan layanan kami😇";
        //}

        // Template notifikasi pesan untuk pembuat pertanyaan jika pertanyaannya telah dijawab
        // Parameter Pembuat Pertanyaan dan Nomor Tiket akan diisi secara dinamis
        //public string BuildTicketTemplate3(string senderName, string ticketNo)
        //{
        //    return $"*[Pertanyaan Berhasil Dibuat]*\n\n" +
        //           $"Halo *{senderName}*🙌,\n\n" +
        //           $"Pertanyaan Anda telah berhasil dibuat dengan *Nomor {ticketNo}*.\n\n" +
        //           $"PIC Keuangan akan segera menindaklanjuti pertanyaan Anda.\n\n" +
        //           $"Terima kasih😇";
        //}

        //public string BuildTicketTemplate1(string ticketNo, string senderName, string unitKerja)
        //{
        //    return $"*[Tindaklanjut Pertanyaan]*\n\n" +
        //           $"Halo *PIC Keuangan*🙌,\n\n" +
        //           $"Pertanyaan baru dengan *Nomor {ticketNo}* telah diajukan oleh:\n" +
        //           $"*Nama: {senderName}*\n" +
        //           $"*Unit Kerja: {unitKerja}*\n\n" +
        //           $"Mohon untuk dapat segera menjawab pertanyaan tersebut.\n\n" +
        //           $"Terima kasih😇";
        //}
    }
}