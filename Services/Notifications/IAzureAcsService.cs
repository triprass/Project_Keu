namespace Project_Keu.Services.Notifications
{
    public interface IAzureAcsService
    {
        Task<string> SendTemplateMessageAsync(
        string toPhoneNumber,
        string templateName,
        string language,
        List<string>? templateParameters = null);

        Task<string> SendTextMessageAsync(
            string toPhoneNumber,
            string messageText);
    }
}