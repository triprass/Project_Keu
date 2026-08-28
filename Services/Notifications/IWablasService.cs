using System;
using System.Threading.Tasks;

namespace Project_Keu.Services.Notifications
{
    public interface IWablasService
    {
        Task<string> SendWhatsAppMessageAsync(string target, string message);
        string BuildTicketTemplate1(string ticketNo, string senderName, string nip, string unitKerja, string kategoriPertanyaan);
        string BuildTicketTemplate2(string ticketNo, string senderName, string nip, string unitKerja, string kategoriPertanyaan);
        string BuildTicketTemplate3(string ticketNo, string senderName, string nip, string unitKerja, string kategoriPertanyaan, Guid Id);
    }
}