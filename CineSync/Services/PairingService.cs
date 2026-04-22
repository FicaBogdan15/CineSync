using CineSync.Models;
using System.Collections.Concurrent;

namespace CineSync.Services
{
    public class PairingService
    {
        private readonly ConcurrentDictionary<string, PairingSession> _sessions = new();

        public string CreateSession(string desktopConnectionId, string? userId)
        {
            CleanExpired();

            var token = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
            var session = new PairingSession
            {
                Token = token,
                DesktopConnectionId = desktopConnectionId,
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };

            _sessions[token] = session;
            return token;
        }

        public PairingSession? GetSession(string token)
        {
            CleanExpired();

            _sessions.TryGetValue(token, out var session);
            if (session != null && session.ExpiresAt < DateTime.UtcNow)
            {
                _sessions.TryRemove(token, out _);
                return null;
            }

            return session;
        }

        public bool MarkPaired(string token, string userId, string? deviceName = null)
        {
            if (_sessions.TryGetValue(token, out var session))
            {
                session.IsPaired = true;
                session.UserId = userId;
                if (!string.IsNullOrWhiteSpace(deviceName))
                {
                    session.DeviceName = deviceName.Trim();
                }
                session.ExpiresAt = DateTime.UtcNow.AddHours(2);
                return true;
            }

            return false;
        }

        public void RemoveSession(string token)
        {
            _sessions.TryRemove(token, out _);
        }

        public IReadOnlyList<PairingSession> GetActiveSessionsForUser(string userId)
        {
            CleanExpired();

            return _sessions.Values
                .Where(session => string.Equals(session.UserId, userId, StringComparison.Ordinal))
                .OrderByDescending(session => session.CreatedAt)
                .ToList();
        }

        private void CleanExpired()
        {
            var expired = _sessions
                .Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expired)
            {
                _sessions.TryRemove(key, out _);
            }
        }
    }
}
