using System.Collections.Concurrent;

namespace Project_Keu.Services
{
    public class OnlineTrackerService
    {
        private readonly ConcurrentDictionary<string, DateTime> _activeUsers = new();

        public int TrackUser(string visitorId)
        {
            if (!string.IsNullOrEmpty(visitorId))
            {
                _activeUsers[visitorId] = DateTime.UtcNow;
            }

            // Hapus koneksi terputus (> 30 detik)
            var expirationTime = DateTime.UtcNow.AddSeconds(-30);
            foreach (var key in _activeUsers.Keys)
            {
                if (_activeUsers.TryGetValue(key, out var lastSeen) && lastSeen < expirationTime)
                {
                    _activeUsers.TryRemove(key, out _);
                }
            }

            return _activeUsers.Count;
        }
    }
}
