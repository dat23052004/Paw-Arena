using System;
using System.Collections.Generic;

namespace PawArena.LiveEvents
{
    /// <summary>Migrates Event progress while preserving valid player achievements.</summary>
    public static class LiveEventProgressMigrator
    {
        /// <summary>The latest Event progress schema version.</summary>
        public const int CurrentVersion = 3;

        /// <summary>Creates or migrates one snapshot for the active Event definition.</summary>
        public static LiveEventProgressSnapshot Migrate(
            LiveEventProgressSnapshot snapshot,
            LiveEventDefinition definition,
            DateTimeOffset now)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var isSameEvent = snapshot != null &&
                              string.Equals(snapshot.EventId, definition.EventId, StringComparison.Ordinal);
            if (!isSameEvent)
            {
                snapshot = new LiveEventProgressSnapshot
                {
                    EventId = definition.EventId,
                    FreeSpinsRemaining = GetFirstWheel(definition)?.MaxFreeSpins ?? 3
                };
            }

            snapshot.Quests ??= new List<LiveEventQuestState>();
            snapshot.ClaimedQuestIds ??= new List<string>();
            snapshot.ClaimedRewardIds ??= new List<string>();
            snapshot.PurchasedShopOfferIds ??= new List<string>();
            snapshot.PurchasedBundleIds ??= new List<string>();
            snapshot.Wheels ??= new List<LiveEventWheelProgressState>();

            if (snapshot.Version < 3 && snapshot.Wheels.Count == 0)
            {
                var firstWheel = GetFirstWheel(definition);
                if (firstWheel != null && !string.IsNullOrWhiteSpace(firstWheel.WheelId))
                {
                    snapshot.Wheels.Add(new LiveEventWheelProgressState
                    {
                        WheelId = firstWheel.WheelId,
                        Pity = Math.Max(0, snapshot.WheelPity),
                        TotalSpins = Math.Max(0, snapshot.TotalSpins)
                    });
                }
            }

            var firstDefinition = GetFirstWheel(definition);
            var maxFreeSpins = firstDefinition?.MaxFreeSpins ?? 3;
            snapshot.FreeSpinsRemaining = Math.Max(
                0,
                Math.Min(maxFreeSpins, snapshot.FreeSpinsRemaining));
            if (snapshot.FreeSpinsRemaining >= maxFreeSpins)
            {
                snapshot.NextFreeSpinAtUnixTimestamp = 0L;
            }
            else if (snapshot.NextFreeSpinAtUnixTimestamp <= 0L)
            {
                var refillMinutes = firstDefinition?.FreeSpinRefillMinutes ?? 15;
                snapshot.NextFreeSpinAtUnixTimestamp = now
                    .AddMinutes(Math.Max(1, refillMinutes))
                    .ToUnixTimeSeconds();
            }

            snapshot.Version = CurrentVersion;
            return snapshot;
        }

        private static LiveEventWheelDefinition GetFirstWheel(LiveEventDefinition definition) =>
            definition.Wheels.Count > 0 ? definition.Wheels[0] : null;
    }
}
