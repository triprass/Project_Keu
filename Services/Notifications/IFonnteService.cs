namespace Project_Keu.Services.Notifications
{
    public interface IFonnteService
    {
        Task<string> SendWhatsAppMessageAsync(string target, string message, string countryCode = "62");
        string BuildTicketTemplate1(string senderName, string ticketNo);
        string BuildTicketTemplate2(string ticketNo, string senderName, string unitKerja);
    }
}
