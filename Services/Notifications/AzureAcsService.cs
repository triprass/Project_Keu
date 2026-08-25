using Azure.Communication.Messages;

namespace Project_Keu.Services.Notifications
{
    public class AzureAcsService : IAzureAcsService
    {
        private readonly NotificationMessagesClient _messagesClient;
        private readonly string _connectionString;
        private readonly Guid _channelRegistrationId;

        // Default Nilai jika tidak terisi di appsettings.json
        private const string DefaultConnectionString = "Endpoint=https://acs-pilar-notification-service.indonesia.communication.azure.com/;Key=YOUR_AZURE_KEY";
        private const string DefaultChannelId = "8d5b2ee9-1581-4917-b73f-22bf44abb2af";

        public AzureAcsService(IConfiguration config)
        {
            _connectionString = config["AzureCommunicationServices:ConnectionString"] ?? DefaultConnectionString;

            var channelIdStr = config["AzureCommunicationServices:ChannelRegistrationId"] ?? DefaultChannelId;
            _channelRegistrationId = Guid.Parse(channelIdStr);

            _messagesClient = new NotificationMessagesClient(_connectionString);
        }

        // 1. Send Notification: jawab_pertanyaan
        // Parameter: {{1}} = No Tiket, {{2}} = Nama, {{3}} = Unit Kerja
        public async Task<bool> SendJawabPertanyaanAsync(
            string picPhone,
            string noTiket,
            string namaPengaju,
            string unitKerja)
        {
            var parameters = new List<string> { noTiket, namaPengaju, unitKerja };
            return await ExecuteSendTemplateAsync(picPhone, "jawab_pertanyaan", "id", parameters);
        }

        // 2. Send Notification: pertanyaan_telah_ditindaklanjuti
        // Parameter: {{1}} = Nama, {{2}} = No Tiket
        public async Task<bool> SendPertanyaanTelahDitindaklanjutiAsync(
            string recipientPhone,
            string namaPengaju,
            string noTiket)
        {
            var parameters = new List<string> { namaPengaju, noTiket };
            return await ExecuteSendTemplateAsync(recipientPhone, "pertanyaan_telah_ditindaklanjuti", "id", parameters);
        }

        // 3. Send Notification: pertanyaan_berhasil_dibuat
        // Parameter: {{1}} = Nama, {{2}} = No Tiket
        public async Task<bool> SendPertanyaanBerhasilDibuatAsync(
            string recipientPhone,
            string namaPengaju,
            string noTiket)
        {
            var parameters = new List<string> { namaPengaju, noTiket };
            return await ExecuteSendTemplateAsync(recipientPhone, "pertanyaan_berhasil_dibuat", "id", parameters);
        }

        // Helper utama untuk mengirimkan payload via Azure SDK
        private async Task<bool> ExecuteSendTemplateAsync(
            string recipientPhone,
            string templateName,
            string languageCode,
            List<string> parameters)
        {
            try
            {
                // Standardisasi nomor HP ke format E.164 (+62...)
                var formattedNumber = recipientPhone.StartsWith("0")
                    ? "+62" + recipientPhone[1..]
                    : (recipientPhone.StartsWith("+") ? recipientPhone : "+" + recipientPhone);

                // Mapping parameter {{1}}, {{2}}, dst.
                var messageTemplateValues = parameters.Select(p => new MessageTemplateText("text", p)).ToList();
                var messageTemplate = new MessageTemplate(templateName, languageCode);

                foreach (var val in messageTemplateValues)
                {
                    messageTemplate.Values.Add(val);
                }

                var content = new TemplateNotificationContent(
                    channelRegistrationId: _channelRegistrationId,
                    to: new List<string> { formattedNumber },
                    template: messageTemplate
                );

                var response = await _messagesClient.SendAsync(content);
                return response?.Value?.Receipts?.FirstOrDefault()?.MessageId != null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ACS Error] Gagal mengirim template '{templateName}': {ex.Message}");
                return false;
            }
        }
    }
}