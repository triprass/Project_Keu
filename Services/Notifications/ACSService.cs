using Azure.Communication.Messages;

namespace Project_Keu.Services.Notifications
{

    public interface IAzureAcsService
    {
        Task<bool> SendPertanyaanBerhasilDibuatAsync(string recipientPhone, string nama, string tanggal, string kategori, string noTiket);
        Task<bool> SendTindaklanjutPertanyaanAsync(string recipientPhone, string noTiket, string status, string catatan);
        Task<bool> SendPertanyaanTelahDitindaklanjutiAsync(string recipientPhone, string nama, string noTiket, string jawaban);
    }

    public class AzureAcsService : IAzureAcsService
    {
        private readonly NotificationMessagesClient _messagesClient;
        private readonly IConfiguration _config;

        public AzureAcsService(IConfiguration config)
        {
            _config = config;
            var connectionString = _config["AzureCommunicationServices:ConnectionString"];
            _messagesClient = new NotificationMessagesClient(connectionString);
        }

        public async Task<bool> SendPertanyaanBerhasilDibuatAsync(string recipientPhone, string nama, string tanggal, string kategori, string noTiket)
        {
            return await ExecuteSendTemplateAsync(recipientPhone, "pertanyaan_berhasil_dibuat", "id", new List<string> { nama, tanggal, kategori, noTiket });
        }

        public async Task<bool> SendTindaklanjutPertanyaanAsync(string recipientPhone, string noTiket, string status, string catatan)
        {
            return await ExecuteSendTemplateAsync(recipientPhone, "tindaklanjut_pertanyaan", "en_US", new List<string> { noTiket, status, catatan });
        }

        public async Task<bool> SendPertanyaanTelahDitindaklanjutiAsync(string recipientPhone, string nama, string noTiket, string jawaban)
        {
            return await ExecuteSendTemplateAsync(recipientPhone, "pertanyaan_telah_ditindaklanjuti", "id", new List<string> { nama, noTiket, jawaban });
        }

        private async Task<bool> ExecuteSendTemplateAsync(string recipientPhone, string templateName, string languageCode, List<string> parameters)
        {
            try
            {
                var channelRegistrationId = Guid.Parse(_config["AzureCommunicationServices:ChannelRegistrationId"]!);

                // Format nomor HP ke standar E.164 (+62...)
                var formattedNumber = recipientPhone.StartsWith("0")
                    ? "+62" + recipientPhone[1..]
                    : (recipientPhone.StartsWith("+") ? recipientPhone : "+" + recipientPhone);

                var messageTemplateValues = parameters.Select(p => new MessageTemplateText("text", p)).ToList();
                var messageTemplate = new MessageTemplate(templateName, languageCode);

                foreach (var val in messageTemplateValues)
                {
                    messageTemplate.Values.Add(val);
                }

                var content = new TemplateNotificationContent(
                    channelRegistrationId: channelRegistrationId,
                    to: new List<string> { formattedNumber },
                    template: messageTemplate
                );

                var response = await _messagesClient.SendAsync(content);
                return response?.Value?.Receipts?.FirstOrDefault()?.MessageId != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ACS Error] Failed sending template {templateName}: {ex.Message}");
                return false;
            }
        }
    }
}
