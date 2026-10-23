using System;

namespace PawArena.Core.Profiles
{
    /// <summary>
    /// Serializable profile statistics displayed by the Stats screen.
    /// </summary>
    [Serializable]
    public sealed class PlayerStatsData
    {
        public int Wins;
        public int Knockouts;
        public int CurrentWinStreak;
        public int BestWinStreak;

        /// <summary>
        /// Creates an independent copy.
        /// </summary>
        public PlayerStatsData Clone()
        {
            return new PlayerStatsData
            {
                Wins = Wins,
                Knockouts = Knockouts,
                CurrentWinStreak = CurrentWinStreak,
                BestWinStreak = BestWinStreak
            };
        }

        /// <summary>
        /// Repairs negative persisted values.
        /// </summary>
        public void Normalize()
        {
            Wins = Math.Max(0, Wins);
            Knockouts = Math.Max(0, Knockouts);
            CurrentWinStreak = Math.Max(0, CurrentWinStreak);
            BestWinStreak = Math.Max(CurrentWinStreak, Math.Max(0, BestWinStreak));
        }
    }
}
