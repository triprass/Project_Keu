namespace Project_Keu.Services.Notifications
{
    public interface IFonnteService
    {
        Task<string> SendWhatsAppMessageAsync(string target, string message, string countryCode = "62");
    }
}
