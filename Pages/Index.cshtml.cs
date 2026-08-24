using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Concurrent;

namespace Project_Keu.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private static readonly ConcurrentDictionary<string, DateTime> ActiveUsers = new();

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {

        }

        public IActionResult OnGetHeartbeat(string id)
        {
            // Jika tidak ada ID, fallback pakai IP address
            string identifier = !string.IsNullOrEmpty(id) ? id : (HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            // Update waktu aktif berdasarkan ID unik browser
            ActiveUsers[identifier] = DateTime.UtcNow;

            // Hapus session yang tidak mengirim heartbeat lebih dari 30 detik (stale connection)
            var expirationTime = DateTime.UtcNow.AddSeconds(-30);
            foreach (var key in ActiveUsers.Keys)
            {
                if (ActiveUsers.TryGetValue(key, out var lastSeen) && lastSeen < expirationTime)
                {
                    ActiveUsers.TryRemove(key, out _);
                }
            }

            return new JsonResult(new { count = ActiveUsers.Count });
        }
    }
}
