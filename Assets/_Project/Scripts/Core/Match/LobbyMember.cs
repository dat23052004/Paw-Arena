using System;

namespace PawArena.Core.Match
{
    /// <summary>
    /// Represents one authoritative lobby member before a match assignment is created.
    /// </summary>
    public sealed class LobbyMember
    {
        /// <summary>
        /// Creates a lobby member.
        /// </summary>
        public LobbyMember(string playerId, bool isBot, bool isAfk = false)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("Player id cannot be empty.", nameof(playerId));
            }

            PlayerId = playerId.Trim();
            IsBot = isBot;
            IsAfk = isAfk;
        }

        /// <summary>Gets the stable player identifier used by the server.</summary>
        public string PlayerId { get; }

        /// <summary>Gets whether this member is controlled by the server.</summary>
        public bool IsBot { get; }

        /// <summary>Gets whether this member must be excluded from matchmaking.</summary>
        public bool IsAfk { get; private set; }

        /// <summary>Updates the server-owned AFK state.</summary>
        public void SetAfk(bool isAfk) => IsAfk = isAfk;
    }
}
