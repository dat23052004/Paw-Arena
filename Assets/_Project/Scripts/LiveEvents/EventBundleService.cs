using System;

namespace PawArena.LiveEvents
{
    /// <summary>Resolves independently scheduled Event bundles.</summary>
    public sealed class EventBundleService
    {
        private readonly LiveEventDefinition _definition;
        private readonly EventSchedulePolicy _schedule;

        /// <summary>Creates a Bundle schedule reader.</summary>
        public EventBundleService(LiveEventDefinition definition, EventSchedulePolicy schedule)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        }

        /// <summary>Finds a configured bundle by stable ID.</summary>
        public LiveEventBundleDefinition Find(string bundleId)
        {
            if (string.IsNullOrWhiteSpace(bundleId))
            {
                return null;
            }

            for (var i = 0; i < _definition.Bundles.Count; i++)
            {
                var bundle = _definition.Bundles[i];
                if (bundle != null && string.Equals(bundle.BundleId, bundleId, StringComparison.Ordinal))
                {
                    return bundle;
                }
            }

            return null;
        }

        /// <summary>Returns whether a configured bundle is active now.</summary>
        public bool IsActive(LiveEventBundleDefinition bundle) =>
            bundle != null && _schedule.IsAvailable(bundle.StartHourOffset, bundle.EndHourOffset);

    }
}
