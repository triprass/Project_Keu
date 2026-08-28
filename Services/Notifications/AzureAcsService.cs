using Azure;
using Azure.Communication.Messages;

namespace Project_Keu.Services.Notifications    
{
    public class AzureAcsService : IAzureAcsService
    {
        private readonly NotificationMessagesClient _messagesClient;
        private readonly Guid _channelRegistrationId;
        private readonly ILogger<AzureAcsService> _logger;

        public AzureAcsService(
            IConfiguration configuration,
            ILogger<AzureAcsService> logger)
        {
            _logger = logger;

            // 1. Membaca ConnectionString dari section AzureCommunicationServices
            string connectionString = configuration["AzureCommunicationServices:ConnectionString"]
                ?? throw new InvalidOperationException("ConnectionString 'AzureCommunicationServices:ConnectionString' tidak ditemukan di appsettings.json.");

            // 2. Inisialisasi NotificationMessagesClient
            _messagesClient = new NotificationMessagesClient(connectionString);

            // 3. Membaca ChannelRegistrationId dari section AzureCommunicationServices
            var registrationIdStr = configuration["AzureCommunicationServices:ChannelRegistrationId"];
            if (!Guid.TryParse(registrationIdStr, out _channelRegistrationId))
            {
                throw new InvalidOperationException("ChannelRegistrationId 'AzureCommunicationServices:ChannelRegistrationId' tidak valid atau kosong di appsettings.json.");
            }
        }

        public async Task<string> SendTemplateMessageAsync(
            string toPhoneNumber,
            string templateName,
            string language,
            List<string>? templateParameters = null)
        {
            try
            {
                var recipientList = new List<string> { toPhoneNumber };
                var template = new MessageTemplate(templateName, language);

                if (templateParameters != null && templateParameters.Count > 0)
                {
                    foreach (var paramValue in templateParameters)
                    {
                        template.Values.Add(new MessageTemplateText(paramValue, paramValue));
                    }
                }

                var content = new TemplateNotificationContent(_channelRegistrationId, recipientList, template);

                var result = await _messagesClient.SendAsync(content);
                var messageId = result.Value.Receipts.FirstOrDefault()?.MessageId ?? string.Empty;

                _logger.LogInformation("Template WhatsApp terkirim ke {Phone} dengan ID: {MessageId}", toPhoneNumber, messageId);

                return messageId;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Gagal mengirim pesan template WhatsApp ke {Phone}", toPhoneNumber);
                throw;
            }
        }

        public async Task<string> SendTextMessageAsync(
            string toPhoneNumber,
            string messageText)
        {
            try
            {
                var recipientList = new List<string> { toPhoneNumber };
                var content = new TextNotificationContent(_channelRegistrationId, recipientList, messageText);

                var result = await _messagesClient.SendAsync(content);
                var messageId = result.Value.Receipts.FirstOrDefault()?.MessageId ?? string.Empty;

                _logger.LogInformation("Pesan teks WhatsApp terkirim ke {Phone} dengan ID: {MessageId}", toPhoneNumber, messageId);

                return messageId;
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Gagal mengirim pesan teks WhatsApp ke {Phone}", toPhoneNumber);
                throw;
            }
        }
    }
}