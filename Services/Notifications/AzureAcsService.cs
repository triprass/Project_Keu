using Azure.Communication.Messages;

namespace PilarKeuangan.Services.Notifications
{
    public class AzureAcsService : IAzureAcsService
    {
        private readonly NotificationMessagesClient _messagesClient;

        private const string ConnectionString = "endpoint=https://acs-pilar-notification-service.indonesia.communication.azure.com/;accesskey=KODE_ACCESS_KEY_AZURE_ANDA";
        private static readonly Guid ChannelRegistrationId = Guid.Parse("8d5b2ee9-1581-4917-b73f-22bf44abb2af");

        public AzureAcsService()
        {
            _messagesClient = new NotificationMessagesClient(ConnectionString);
        }

        public async Task<bool> SendPertanyaanBerhasilDibuatAsync(string recipientPhone, string nama, string tanggal, string kategori, string noTiket)
        {
            var parameters = new List<string> { nama, tanggal, kategori, noTiket };
            return await ExecuteSendTemplateAsync(recipientPhone, "pertanyaan_berhasil_dibuat", "id", parameters);
        }

        public async Task<bool> SendTindaklanjutPertanyaanAsync(string recipientPhone, string noTiket, string status, string catatan)
        {
            var parameters = new List<string> { noTiket, status, catatan };
            return await ExecuteSendTemplateAsync(recipientPhone, "tindaklanjut_pertanyaan", "en_US", parameters);
        }

        public async Task<bool> SendPertanyaanTelahDitindaklanjutiAsync(string recipientPhone, string nama, string noTiket, string jawaban)
        {
            var parameters = new List<string> { nama, noTiket, jawaban };
            return await ExecuteSendTemplateAsync(recipientPhone, "pertanyaan_telah_ditindaklanjuti", "id", parameters);
        }

        private async Task<bool> ExecuteSendTemplateAsync(string recipientPhone, string templateName, string languageCode, List<string> parameters)
        {
            try
            {
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
                    channelRegistrationId: ChannelRegistrationId,
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