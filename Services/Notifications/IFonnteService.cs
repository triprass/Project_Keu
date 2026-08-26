namespace Project_Keu.Services.Notifications
{
    public interface IFonnteService
    {
        Task<string> SendWhatsAppMessageAsync(string target, string message, string countryCode = "62");
        Task<string> SendTicketNotificationAsync(string targetPhone, string picName, string ticketNo, string senderName, string unitKerja);
        string BuildTicketTemplate(string picName, string ticketNo, string senderName, string unitKerja);
    }
}
