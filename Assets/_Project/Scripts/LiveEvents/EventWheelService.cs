using System;
using PawArena.Core.Economy;
using PawArena.Core.Time;

namespace PawArena.LiveEvents
{
    /// <summary>Owns shared Free Spins, paid packs, outcomes and per-Wheel pity.</summary>
    public sealed class EventWheelService
    {
        private readonly string _playerId;
        private readonly string _eventId;
        private readonly EventWheelScheduleService _schedule;
        private readonly LiveEventProgressSnapshot _snapshot;
        private readonly IClock _clock;
        private readonly IWheelTicketService _tickets;
        private readonly EventPurchaseCoordinator _purchaseCoordinator;
        private string _pendingPackOperationId;
        private string _pendingPackResetKey;
        private LiveEventWheelPackQuote _pendingPackQuote;

        public EventWheelService(
            string playerId,
            string eventId,
            EventWheelScheduleService schedule,
            LiveEventProgressSnapshot snapshot,
            IClock clock,
            IWheelTicketService tickets,
            EventPurchaseCoordinator purchaseCoordinator)
        {
            _playerId = string.IsNullOrWhiteSpace(playerId)
                ? throw new ArgumentException("Player ID is required.", nameof(playerId))
                : playerId;
            _eventId = string.IsNullOrWhiteSpace(eventId)
                ? throw new ArgumentException("Event ID is required.", nameof(eventId))
                : eventId;
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
            _purchaseCoordinator = purchaseCoordinator ??
                                   throw new ArgumentNullException(nameof(purchaseCoordinator));
        }

        public LiveEventWheelDefinition ActiveWheel => _schedule.ActiveWheel;
        public int FreeSpinsRemaining => _snapshot.FreeSpinsRemaining;
        public long PaidSpinsRemaining => _tickets.Balance;
        public int Pity => FindProgress(ActiveWheel, false)?.Pity ?? 0;

        public TimeSpan NextFreeSpinTimeRemaining
        {
            get
            {
                var definition = ActiveWheel ?? _schedule.FirstWheel;
                if (definition == null ||
                    _snapshot.FreeSpinsRemaining >= definition.MaxFreeSpins ||
                    _snapshot.NextFreeSpinAtUnixTimestamp <= 0L)
                {
                    return TimeSpan.Zero;
                }

                var next = DateTimeOffset.FromUnixTimeSeconds(
                    _snapshot.NextFreeSpinAtUnixTimestamp);
                var remaining = next - _clock.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        public bool ReconcileFreeSpins()
        {
            var definition = ActiveWheel ?? _schedule.FirstWheel;
            if (definition == null)
            {
                return false;
            }

            var max = definition.MaxFreeSpins;
            var previousSpins = _snapshot.FreeSpinsRemaining;
            var previousNext = _snapshot.NextFreeSpinAtUnixTimestamp;
            _snapshot.FreeSpinsRemaining = Math.Max(0, Math.Min(max, _snapshot.FreeSpinsRemaining));

            if (_snapshot.FreeSpinsRemaining >= max)
            {
                _snapshot.NextFreeSpinAtUnixTimestamp = 0L;
                return previousSpins != _snapshot.FreeSpinsRemaining || previousNext != 0L;
            }

            var intervalSeconds = checked(definition.FreeSpinRefillMinutes * 60L);
            var nowSeconds = _clock.UtcNow.ToUnixTimeSeconds();
            if (_snapshot.NextFreeSpinAtUnixTimestamp <= 0L)
            {
                _snapshot.NextFreeSpinAtUnixTimestamp = checked(nowSeconds + intervalSeconds);
                return true;
            }

            if (nowSeconds < _snapshot.NextFreeSpinAtUnixTimestamp)
            {
                return previousSpins != _snapshot.FreeSpinsRemaining;
            }

            var elapsedAfterFirst = nowSeconds - _snapshot.NextFreeSpinAtUnixTimestamp;
            var elapsedIntervals = 1L + elapsedAfterFirst / intervalSeconds;
            var missing = max - _snapshot.FreeSpinsRemaining;
            var granted = (int)Math.Min(missing, elapsedIntervals);
            _snapshot.FreeSpinsRemaining += granted;
            if (_snapshot.FreeSpinsRemaining >= max)
            {
                _snapshot.NextFreeSpinAtUnixTimestamp = 0L;
            }
            else
            {
                _snapshot.NextFreeSpinAtUnixTimestamp = checked(
                    _snapshot.NextFreeSpinAtUnixTimestamp + elapsedIntervals * intervalSeconds);
            }

            return previousSpins != _snapshot.FreeSpinsRemaining ||
                   previousNext != _snapshot.NextFreeSpinAtUnixTimestamp;
        }

        public LiveEventWheelPackQuote GetPackQuote(int spinCount, string resetKey)
        {
            var definition = ActiveWheel;
            if (definition == null)
            {
                return default;
            }

            var useDailyDeal = spinCount == 1 && definition.DailyDealEnabled &&
                               _tickets.IsBuyOneDailyDealAvailable(_eventId, resetKey);
            var price = spinCount switch
            {
                1 when useDailyDeal => definition.BuyOnePrice,
                1 => definition.BuyOneOriginalPrice,
                3 => definition.BuyThreePrice,
                10 => definition.BuyTenPrice,
                _ => 0L
            };
            return new LiveEventWheelPackQuote(spinCount, price, useDailyDeal);
        }

        public LiveEventOperationResult TryPurchasePack(int spinCount, string resetKey)
        {
            if (ActiveWheel == null)
            {
                return LiveEventOperationResult.EventInactive;
            }

            if (!string.IsNullOrWhiteSpace(_pendingPackOperationId))
            {
                if (_pendingPackQuote.SpinCount != spinCount ||
                    !string.Equals(_pendingPackResetKey, resetKey, StringComparison.Ordinal))
                {
                    return LiveEventOperationResult.PersistenceFailed;
                }

                var retryResult = _purchaseCoordinator.TryPurchaseWheelPack(
                    _eventId,
                    _pendingPackResetKey,
                    _pendingPackOperationId,
                    _pendingPackQuote);
                if (retryResult == LiveEventOperationResult.Success)
                {
                    ClearPendingPack();
                }

                return retryResult;
            }

            var quote = GetPackQuote(spinCount, resetKey);
            if (!quote.IsValid)
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            var operationId = $"live-event:{_eventId}:wheel-pack:{Guid.NewGuid():N}";
            var result = _purchaseCoordinator.TryPurchaseWheelPack(
                _eventId,
                resetKey,
                operationId,
                quote);
            if (result == LiveEventOperationResult.PersistenceFailed)
            {
                _pendingPackOperationId = operationId;
                _pendingPackResetKey = resetKey;
                _pendingPackQuote = quote;
            }

            return result;
        }

        public LiveEventSpinResult TrySpin()
        {
            var definition = ActiveWheel;
            if (definition == null)
            {
                return new LiveEventSpinResult(LiveEventOperationResult.EventInactive, null);
            }

            ReconcileFreeSpins();
            var source = _snapshot.FreeSpinsRemaining > 0
                ? LiveEventWheelSpinSource.Free
                : _tickets.Balance > 0L
                    ? LiveEventWheelSpinSource.PaidTicket
                    : LiveEventWheelSpinSource.None;
            if (source == LiveEventWheelSpinSource.None)
            {
                return new LiveEventSpinResult(
                    LiveEventOperationResult.NoSpinAvailable,
                    null,
                    source);
            }

            var progress = FindProgress(definition, true);
            var nextSpin = checked(progress.TotalSpins + 1);
            var prize = SelectPrize(definition, progress, nextSpin);
            if (prize == null)
            {
                return new LiveEventSpinResult(
                    LiveEventOperationResult.InvalidConfiguration,
                    null,
                    source);
            }

            var operation = _purchaseCoordinator.TryCompleteWheelSpin(
                _eventId,
                definition.WheelId,
                nextSpin,
                source == LiveEventWheelSpinSource.PaidTicket,
                prize);
            if (operation != LiveEventOperationResult.Success)
            {
                return new LiveEventSpinResult(operation, null, source);
            }

            if (source == LiveEventWheelSpinSource.Free)
            {
                _snapshot.FreeSpinsRemaining--;
                if (_snapshot.FreeSpinsRemaining < definition.MaxFreeSpins &&
                    _snapshot.NextFreeSpinAtUnixTimestamp <= 0L)
                {
                    _snapshot.NextFreeSpinAtUnixTimestamp = _clock.UtcNow
                        .AddMinutes(definition.FreeSpinRefillMinutes)
                        .ToUnixTimeSeconds();
                }
            }

            progress.TotalSpins = nextSpin;
            progress.Pity = string.Equals(
                prize.PrizeId,
                definition.FeaturedPrizeId,
                StringComparison.Ordinal)
                ? 0
                : Math.Min(definition.PityThreshold, progress.Pity + 1);
            _snapshot.TotalSpins = progress.TotalSpins;
            _snapshot.WheelPity = progress.Pity;
            return new LiveEventSpinResult(LiveEventOperationResult.Success, prize, source);
        }

        private LiveEventWheelProgressState FindProgress(
            LiveEventWheelDefinition definition,
            bool create)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.WheelId))
            {
                return null;
            }

            _snapshot.Wheels ??= new System.Collections.Generic.List<LiveEventWheelProgressState>();
            for (var i = 0; i < _snapshot.Wheels.Count; i++)
            {
                var progress = _snapshot.Wheels[i];
                if (progress != null && string.Equals(
                        progress.WheelId,
                        definition.WheelId,
                        StringComparison.Ordinal))
                {
                    return progress;
                }
            }

            if (!create)
            {
                return null;
            }

            var created = new LiveEventWheelProgressState { WheelId = definition.WheelId };
            _snapshot.Wheels.Add(created);
            return created;
        }

        private LiveEventWheelPrizeDefinition SelectPrize(
            LiveEventWheelDefinition definition,
            LiveEventWheelProgressState progress,
            int spinNumber)
        {
            if (definition.Prizes.Count == 0)
            {
                return null;
            }

            if (progress.Pity + 1 >= definition.PityThreshold)
            {
                for (var i = 0; i < definition.Prizes.Count; i++)
                {
                    var prize = definition.Prizes[i];
                    if (prize != null && string.Equals(
                            prize.PrizeId,
                            definition.FeaturedPrizeId,
                            StringComparison.Ordinal))
                    {
                        return prize;
                    }
                }
            }

            var totalWeight = 0;
            for (var i = 0; i < definition.Prizes.Count; i++)
            {
                if (definition.Prizes[i] != null)
                {
                    totalWeight = checked(totalWeight + definition.Prizes[i].Weight);
                }
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            var seed = $"{_playerId}:{_eventId}:{definition.WheelId}:{spinNumber}";
            var roll = (int)(StableHash(seed) % (uint)totalWeight);
            for (var i = 0; i < definition.Prizes.Count; i++)
            {
                var prize = definition.Prizes[i];
                if (prize == null)
                {
                    continue;
                }

                if (roll < prize.Weight)
                {
                    return prize;
                }

                roll -= prize.Weight;
            }

            return null;
        }

        private static uint StableHash(string value)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;
            for (var i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= prime;
            }

            return hash;
        }

        private void ClearPendingPack()
        {
            _pendingPackOperationId = null;
            _pendingPackResetKey = null;
            _pendingPackQuote = default;
        }
    }
}
