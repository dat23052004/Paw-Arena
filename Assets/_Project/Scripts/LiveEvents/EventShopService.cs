using System;
using System.Collections.Generic;

namespace PawArena.LiveEvents
{
    /// <summary>Resolves the one contiguous Shop period active at a given Event time.</summary>
    public sealed class EventShopService
    {
        private static readonly LiveEventShopOfferDefinition[] EmptyOffers =
            Array.Empty<LiveEventShopOfferDefinition>();

        private readonly LiveEventDefinition _definition;
        private readonly EventSchedulePolicy _schedule;

        /// <summary>Creates a Shop schedule reader.</summary>
        public EventShopService(LiveEventDefinition definition, EventSchedulePolicy schedule)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        }

        /// <summary>Gets the active period, or null outside the Event.</summary>
        public LiveEventShopPeriodDefinition ActivePeriod
        {
            get
            {
                if (!_schedule.IsActive)
                {
                    return null;
                }

                var startOffset = 0;
                for (var i = 0; i < _definition.ShopPeriods.Count; i++)
                {
                    var period = _definition.ShopPeriods[i];
                    if (period != null && _schedule.IsAvailable(startOffset, period.EndHourOffset))
                    {
                        return period;
                    }

                    if (period != null)
                    {
                        startOffset = period.EndHourOffset;
                    }
                }

                return null;
            }
        }

        /// <summary>Gets the active period offers without allocating.</summary>
        public IReadOnlyList<LiveEventShopOfferDefinition> ActiveOffers =>
            ActivePeriod?.Offers ?? EmptyOffers;

        /// <summary>Gets remaining time in the current period.</summary>
        public TimeSpan RotationTimeRemaining => ActivePeriod == null
            ? TimeSpan.Zero
            : _schedule.GetAvailabilityTimeRemaining(ActivePeriod.EndHourOffset);

        /// <summary>Finds an offer across all configured periods.</summary>
        public LiveEventShopOfferDefinition FindOffer(string offerId)
        {
            if (string.IsNullOrWhiteSpace(offerId))
            {
                return null;
            }

            var offers = _definition.ShopOffers;
            for (var i = 0; i < offers.Count; i++)
            {
                var offer = offers[i];
                if (offer != null && string.Equals(offer.OfferId, offerId, StringComparison.Ordinal))
                {
                    return offer;
                }
            }

            return null;
        }

        /// <summary>Returns whether one offer belongs to the currently active period.</summary>
        public bool IsOfferActive(string offerId)
        {
            var offers = ActiveOffers;
            for (var i = 0; i < offers.Count; i++)
            {
                if (offers[i] != null &&
                    string.Equals(offers[i].OfferId, offerId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
