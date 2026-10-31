using System;
using System.Collections.Generic;
using PawArena.Core.Economy;
using PawArena.Core.Inventory;

namespace PawArena.LiveEvents
{
    /// <summary>Validates live-ops configuration before any purchase or progress is accepted.</summary>
    public static class LiveEventDefinitionValidator
    {
        /// <summary>Returns every detected configuration error.</summary>
        public static IReadOnlyList<string> Validate(
            LiveEventDefinition definition,
            IItemCatalog catalog = null)
        {
            var errors = new List<string>();
            if (definition == null)
            {
                errors.Add("LiveEventDefinition is missing.");
                return errors;
            }

            if (string.IsNullOrWhiteSpace(definition.EventId))
            {
                errors.Add("EventId is required.");
            }

            if (!definition.TryGetSchedule(out var startsAt, out var endsAt))
            {
                errors.Add("Event UTC schedule is invalid.");
                return errors;
            }

            ValidateShop(definition, catalog, startsAt, endsAt, errors);
            ValidateWheels(definition, catalog, startsAt, endsAt, errors);
            ValidateBundles(definition, catalog, startsAt, endsAt, errors);
            return errors;
        }

        private static void ValidateShop(
            LiveEventDefinition definition,
            IItemCatalog catalog,
            DateTimeOffset startsAt,
            DateTimeOffset endsAt,
            ICollection<string> errors)
        {
            if (definition.ShopPeriods.Count == 0)
            {
                errors.Add("Shop must contain at least one contiguous period.");
                return;
            }

            var offerIds = new HashSet<string>(StringComparer.Ordinal);
            var periodIds = new HashSet<string>(StringComparer.Ordinal);
            var previousEnd = 0;
            for (var periodIndex = 0; periodIndex < definition.ShopPeriods.Count; periodIndex++)
            {
                var period = definition.ShopPeriods[periodIndex];
                if (period == null)
                {
                    errors.Add($"Shop period {periodIndex} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(period.PeriodId))
                {
                    errors.Add($"Shop period {periodIndex} requires a stable PeriodId.");
                }
                else if (!periodIds.Add(period.PeriodId))
                {
                    errors.Add($"Duplicate Shop PeriodId '{period.PeriodId}'.");
                }

                if (period.EndHourOffset <= previousEnd)
                {
                    errors.Add($"Shop period '{period.PeriodId}' must end after the previous period.");
                }

                if (period.Offers.Count < 1 || period.Offers.Count > 4)
                {
                    errors.Add($"Shop period '{period.PeriodId}' must contain 1 to 4 offers.");
                }

                for (var offerIndex = 0; offerIndex < period.Offers.Count; offerIndex++)
                {
                    var offer = period.Offers[offerIndex];
                    if (offer == null || string.IsNullOrWhiteSpace(offer.OfferId))
                    {
                        errors.Add($"Shop period '{period.PeriodId}' contains an offer without an ID.");
                        continue;
                    }

                    if (!offerIds.Add(offer.OfferId))
                    {
                        errors.Add($"Duplicate Shop OfferId '{offer.OfferId}'.");
                    }

                    if (string.IsNullOrWhiteSpace(offer.ItemId) ||
                        catalog != null && !catalog.TryGet(offer.ItemId, out _))
                    {
                        errors.Add($"Shop offer '{offer.OfferId}' references an unknown item.");
                    }
                }

                previousEnd = period.EndHourOffset;
            }

            var durationHours = (endsAt - startsAt).TotalHours;
            if (Math.Abs(durationHours - Math.Round(durationHours)) > 0.0001d)
            {
                errors.Add("Event duration must resolve to whole hours for Shop period offsets.");
            }
            else if (previousEnd != (int)Math.Round(durationHours))
            {
                errors.Add("The final Shop period must end exactly at Event end.");
            }
        }

        private static void ValidateWheels(
            LiveEventDefinition definition,
            IItemCatalog catalog,
            DateTimeOffset startsAt,
            DateTimeOffset endsAt,
            ICollection<string> errors)
        {
            if (definition.Wheels.Count == 0)
            {
                errors.Add("Event must contain at least one scheduled Wheel.");
                return;
            }

            var durationHours = (endsAt - startsAt).TotalHours;
            if (Math.Abs(durationHours - Math.Round(durationHours)) > 0.0001d)
            {
                errors.Add("Event duration must resolve to whole hours for Wheel schedules.");
                return;
            }

            var wheelIds = new HashSet<string>(StringComparer.Ordinal);
            var previousEnd = 0;
            for (var wheelIndex = 0; wheelIndex < definition.Wheels.Count; wheelIndex++)
            {
                var wheel = definition.Wheels[wheelIndex];
                if (wheel == null)
                {
                    errors.Add($"Wheel {wheelIndex} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(wheel.WheelId))
                {
                    errors.Add($"Wheel {wheelIndex} requires a stable WheelId.");
                }
                else if (!wheelIds.Add(wheel.WheelId))
                {
                    errors.Add($"Duplicate WheelId '{wheel.WheelId}'.");
                }

                if (string.IsNullOrWhiteSpace(wheel.DisplayName))
                {
                    errors.Add($"Wheel '{wheel.WheelId}' requires a display name.");
                }

                if (wheel.Presentation != null &&
                    string.IsNullOrWhiteSpace(wheel.Presentation.PresentationId))
                {
                    errors.Add($"Wheel '{wheel.WheelId}' presentation requires a stable ID.");
                }

                if (wheel.StartHourOffset != previousEnd)
                {
                    errors.Add($"Wheel '{wheel.WheelId}' must start at hour {previousEnd} so the schedule has no gap or overlap.");
                }

                if (wheel.EndHourOffset <= wheel.StartHourOffset)
                {
                    errors.Add($"Wheel '{wheel.WheelId}' has an invalid Event window.");
                }

                ValidateWheelEconomy(wheel, catalog, errors);
                previousEnd = wheel.EndHourOffset;
            }

            if (previousEnd != (int)Math.Round(durationHours))
            {
                errors.Add("The final Wheel must end exactly at Event end.");
            }
        }

        private static void ValidateWheelEconomy(
            LiveEventWheelDefinition wheel,
            IItemCatalog catalog,
            ICollection<string> errors)
        {
            var owner = string.IsNullOrWhiteSpace(wheel.WheelId)
                ? "Wheel"
                : $"Wheel '{wheel.WheelId}'";

            if (wheel.MaxFreeSpins != 3)
            {
                errors.Add(owner + " MaxFreeSpins must be 3.");
            }

            if (wheel.FreeSpinRefillMinutes != 15)
            {
                errors.Add(owner + " FreeSpinRefillMinutes must be 15.");
            }

            if (wheel.PackCurrency != CurrencyType.Premium)
            {
                errors.Add(owner + " paid packs must use Premium.");
            }

            if (wheel.BuyOneOriginalPrice <= 0L || wheel.BuyOnePrice <= 0L ||
                wheel.BuyThreePrice <= 0L || wheel.BuyTenPrice <= 0L)
            {
                errors.Add(owner + " Buy 1/3/10 prices must be positive.");
            }

            if (wheel.BuyOnePrice * 2L != wheel.BuyOneOriginalPrice)
            {
                errors.Add(owner + " Buy 1 daily deal must be exactly 50% of its full price.");
            }

            var prizeIds = new HashSet<string>(StringComparer.Ordinal);
            var totalWeight = 0;
            for (var i = 0; i < wheel.Prizes.Count; i++)
            {
                var prize = wheel.Prizes[i];
                if (prize == null || string.IsNullOrWhiteSpace(prize.PrizeId))
                {
                    errors.Add($"{owner} prize {i} requires a stable ID.");
                    continue;
                }

                if (!prizeIds.Add(prize.PrizeId))
                {
                    errors.Add($"{owner} has duplicate PrizeId '{prize.PrizeId}'.");
                }

                totalWeight += prize.Weight;
                ValidateGrant(prize.Grant, catalog, $"{owner} prize '{prize.PrizeId}'", errors);
            }

            if (totalWeight <= 0)
            {
                errors.Add(owner + " total prize weight must be positive.");
            }

            if (!string.IsNullOrWhiteSpace(wheel.FeaturedPrizeId) &&
                !prizeIds.Contains(wheel.FeaturedPrizeId))
            {
                errors.Add(owner + " FeaturedPrizeId does not exist in the prize list.");
            }
        }

        private static void ValidateBundles(
            LiveEventDefinition definition,
            IItemCatalog catalog,
            DateTimeOffset startsAt,
            DateTimeOffset endsAt,
            ICollection<string> errors)
        {
            var bundleIds = new HashSet<string>(StringComparer.Ordinal);
            var eventHours = (int)Math.Ceiling((endsAt - startsAt).TotalHours);
            if (definition.Bundles.Count == 0)
            {
                errors.Add("Event must contain at least one Bundle.");
            }

            for (var i = 0; i < definition.Bundles.Count; i++)
            {
                var bundle = definition.Bundles[i];
                if (bundle == null || string.IsNullOrWhiteSpace(bundle.BundleId))
                {
                    errors.Add($"Bundle {i} requires a stable ID.");
                    continue;
                }

                if (!bundleIds.Add(bundle.BundleId))
                {
                    errors.Add($"Duplicate BundleId '{bundle.BundleId}'.");
                }

                if (bundle.Presentation != null &&
                    string.IsNullOrWhiteSpace(bundle.Presentation.PresentationId))
                {
                    errors.Add($"Bundle '{bundle.BundleId}' presentation requires a stable ID.");
                }

                if (bundle.StartHourOffset >= bundle.EndHourOffset ||
                    bundle.EndHourOffset > eventHours)
                {
                    errors.Add($"Bundle '{bundle.BundleId}' has an invalid Event window.");
                }

                if (bundle.Grants.Count == 0)
                {
                    errors.Add($"Bundle '{bundle.BundleId}' requires at least one grant.");
                }

                for (var grantIndex = 0; grantIndex < bundle.Grants.Count; grantIndex++)
                {
                    ValidateGrant(
                        bundle.Grants[grantIndex],
                        catalog,
                        $"Bundle '{bundle.BundleId}' grant {grantIndex}",
                        errors);
                }
            }
        }

        private static void ValidateGrant(
            LiveEventGrantDefinition grant,
            IItemCatalog catalog,
            string owner,
            ICollection<string> errors)
        {
            if (grant == null)
            {
                errors.Add(owner + " is missing its grant.");
                return;
            }

            if (grant.Kind == LiveEventGrantKind.Item &&
                (string.IsNullOrWhiteSpace(grant.ItemId) ||
                 catalog != null && !catalog.TryGet(grant.ItemId, out _)))
            {
                errors.Add(owner + " references an unknown item.");
            }
        }
    }
}
