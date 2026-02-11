using System.Collections.Concurrent;

namespace Bagrut_Eval.Utilities
{
    public static class LoggedInUsers
    {
        // Using ConcurrentDictionary for thread-safe access
        private static ConcurrentDictionary<int, DateTime> _activeUsers = new ConcurrentDictionary<int, DateTime>();

        public static void AddUser(int userId)
        {
            _activeUsers.AddOrUpdate(userId, DateTime.UtcNow, (key, oldValue) => DateTime.UtcNow);
        }

        public static void RemoveUser(int userId)
        {
            _activeUsers.TryRemove(userId, out _);
        }

        public static List<int> GetActiveUserIds()
        {
            // Clean up users who haven't been active in a while (e.g., 30 minutes)
            var inactiveUsers = _activeUsers.Where(kv => (DateTime.UtcNow - kv.Value).TotalMinutes > 30).ToList();
            foreach (var user in inactiveUsers)
            {
                RemoveUser(user.Key);
            }

            return _activeUsers.Keys.ToList();
        }
    }
}