using System;
using System.Collections.Generic;
using PawArena.Core.Economy;
using PawArena.Core.Inventory;

namespace PawArena.LiveEvents
{
    /// <summary>Coordinates idempotent Event payments, grants and player-profile persistence.</summary>
    public sealed class EventPurchaseCoordinator
    {
        private readonly IWalletService _wallet;
        private readonly IInventoryService _inventory;
        private readonly IItemCatalog _catalog;
        private readonly IWheelTicketService _wheelTickets;
        private readonly Action _persistProfile;

        /// <summary>Creates a coordinator over the active player's profile services.</summary>
        public EventPurchaseCoordinator(
            IWalletService wallet,
            IInventoryService inventory,
            IItemCatalog catalog,
            IWheelTicketService wheelTickets,
            Action persistProfile = null)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _wheelTickets = wheelTickets ?? throw new ArgumentNullException(nameof(wheelTickets));
            _persistProfile = persistProfile;
        }

        /// <summary>Purchases one unique Shop entitlement.</summary>
        public LiveEventOperationResult TryPurchaseShopOffer(
            string eventId,
            LiveEventShopOfferDefinition offer)
        {
            if (offer == null || string.IsNullOrWhiteSpace(offer.ItemId) ||
                !_catalog.TryGet(offer.ItemId, out _))
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            if (_inventory.IsOwned(offer.ItemId))
            {
                return LiveEventOperationResult.AlreadyPurchased;
            }

            var operationId = BuildOperationId(eventId, "shop", offer.OfferId);
            var payment = TryPay(operationId + ":payment", offer.PriceCurrency, offer.Price, offer.OfferId);
            if (payment != LiveEventOperationResult.Success)
            {
                return payment;
            }

            var grant = _inventory.TryGrant(offer.ItemId);
            if (grant != InventoryOperationResult.Success && grant != InventoryOperationResult.AlreadyOwned)
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            return TryPersistProfile();
        }

        /// <summary>Purchases one Bundle and grants all preflighted contents.</summary>
        public LiveEventOperationResult TryPurchaseBundle(
            string eventId,
            LiveEventBundleDefinition bundle)
        {
            if (bundle == null || !AreGrantsValid(bundle.Grants) ||
                !CanApplyGrants(bundle.Grants, bundle.PriceCurrency, bundle.SalePrice))
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            var operationId = BuildOperationId(eventId, "bundle", bundle.BundleId);
            var payment = TryPay(
                operationId + ":payment",
                bundle.PriceCurrency,
                bundle.SalePrice,
                bundle.BundleId);
            if (payment != LiveEventOperationResult.Success)
            {
                return payment;
            }

            for (var i = 0; i < bundle.Grants.Count; i++)
            {
                var grantResult = TryApplyGrant(
                    operationId + ":grant:" + i,
                    bundle.Grants[i],
                    bundle.BundleId);
                if (grantResult != LiveEventOperationResult.Success)
                {
                    return grantResult;
                }
            }

            return TryPersistProfile();
        }

        /// <summary>Buys a repeatable paid-spin pack with Premium.</summary>
        public LiveEventOperationResult TryPurchaseWheelPack(
            string eventId,
            string resetKey,
            string operationId,
            LiveEventWheelPackQuote quote)
        {
            if (!quote.IsValid || quote.PremiumPrice <= 0L ||
                string.IsNullOrWhiteSpace(operationId) ||
                _wheelTickets.Balance > long.MaxValue - quote.SpinCount)
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            var payment = TryPay(
                operationId + ":payment",
                CurrencyType.Premium,
                quote.PremiumPrice,
                "wheel-pack-" + quote.SpinCount);
            if (payment != LiveEventOperationResult.Success)
            {
                return payment;
            }

            var ticketResult = _wheelTickets.TryApply(
                operationId + ":tickets",
                quote.SpinCount,
                out _);
            if (ticketResult != WheelTicketTransactionResult.Success &&
                ticketResult != WheelTicketTransactionResult.DuplicateTransaction)
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            if (quote.UsesDailyDeal)
            {
                _wheelTickets.MarkBuyOneDailyDealUsed(eventId, resetKey);
            }

            return TryPersistProfile();
        }

        /// <summary>Consumes an optional paid ticket and grants one deterministic Wheel prize.</summary>
        public LiveEventOperationResult TryCompleteWheelSpin(
            string eventId,
            string wheelId,
            int spinNumber,
            bool usePaidTicket,
            LiveEventWheelPrizeDefinition prize)
        {
            if (string.IsNullOrWhiteSpace(wheelId) || spinNumber <= 0 || prize == null ||
                !IsGrantValid(prize.Grant) || !CanApplyGrant(prize.Grant))
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            var operationId = BuildOperationId(
                eventId,
                "wheel-spin",
                wheelId + ":" + spinNumber);
            if (usePaidTicket)
            {
                var ticketResult = _wheelTickets.TryApply(
                    operationId + ":ticket",
                    -1L,
                    out _);
                if (ticketResult == WheelTicketTransactionResult.InsufficientTickets)
                {
                    return LiveEventOperationResult.NoSpinAvailable;
                }

                if (ticketResult != WheelTicketTransactionResult.Success &&
                    ticketResult != WheelTicketTransactionResult.DuplicateTransaction)
                {
                    return LiveEventOperationResult.InvalidConfiguration;
                }
            }

            var grantResult = TryApplyGrant(operationId + ":grant", prize.Grant, prize.PrizeId);
            if (grantResult != LiveEventOperationResult.Success)
            {
                return grantResult;
            }

            return TryPersistProfile();
        }

        private LiveEventOperationResult TryPay(
            string transactionId,
            CurrencyType currency,
            long amount,
            string sourceId)
        {
            if (amount < 0L)
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            if (amount == 0L)
            {
                return LiveEventOperationResult.Success;
            }

            var result = _wallet.TryApply(
                transactionId,
                currency,
                -amount,
                "live_event_purchase",
                sourceId,
                out _);
            return result switch
            {
                WalletTransactionResult.Success => LiveEventOperationResult.Success,
                WalletTransactionResult.DuplicateTransaction => LiveEventOperationResult.Success,
                WalletTransactionResult.InsufficientFunds => LiveEventOperationResult.InsufficientFunds,
                _ => LiveEventOperationResult.InvalidConfiguration
            };
        }

        private LiveEventOperationResult TryApplyGrant(
            string transactionId,
            LiveEventGrantDefinition grant,
            string sourceId)
        {
            if (!IsGrantValid(grant))
            {
                return LiveEventOperationResult.InvalidConfiguration;
            }

            if (grant.Kind == LiveEventGrantKind.Currency)
            {
                var walletResult = _wallet.TryApply(
                    transactionId,
                    grant.Currency,
                    grant.Amount,
                    "live_event_reward",
                    sourceId,
                    out _);
                return walletResult == WalletTransactionResult.Success ||
                       walletResult == WalletTransactionResult.DuplicateTransaction
                    ? LiveEventOperationResult.Success
                    : LiveEventOperationResult.InvalidConfiguration;
            }

            var inventoryResult = _inventory.TryGrant(grant.ItemId);
            return inventoryResult == InventoryOperationResult.Success ||
                   inventoryResult == InventoryOperationResult.AlreadyOwned
                ? LiveEventOperationResult.Success
                : LiveEventOperationResult.InvalidConfiguration;
        }

        private bool AreGrantsValid(IReadOnlyList<LiveEventGrantDefinition> grants)
        {
            if (grants == null || grants.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < grants.Count; i++)
            {
                if (!IsGrantValid(grants[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanApplyGrants(
            IReadOnlyList<LiveEventGrantDefinition> grants,
            CurrencyType paymentCurrency,
            long paymentAmount)
        {
            var currencyDeltas = new Dictionary<CurrencyType, long>();
            try
            {
                currencyDeltas[paymentCurrency] = checked(-paymentAmount);
                for (var i = 0; i < grants.Count; i++)
                {
                    var grant = grants[i];
                    if (grant.Kind != LiveEventGrantKind.Currency)
                    {
                        continue;
                    }

                    currencyDeltas.TryGetValue(grant.Currency, out var currentDelta);
                    currencyDeltas[grant.Currency] = checked(currentDelta + grant.Amount);
                }

                foreach (var entry in currencyDeltas)
                {
                    _ = checked(_wallet.GetBalance(entry.Key) + entry.Value);
                }
            }
            catch (OverflowException)
            {
                return false;
            }

            return true;
        }

        private bool CanApplyGrant(LiveEventGrantDefinition grant)
        {
            if (grant.Kind != LiveEventGrantKind.Currency)
            {
                return true;
            }

            try
            {
                _ = checked(_wallet.GetBalance(grant.Currency) + grant.Amount);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private bool IsGrantValid(LiveEventGrantDefinition grant)
        {
            return grant != null &&
                   (grant.Kind == LiveEventGrantKind.Currency ||
                    !string.IsNullOrWhiteSpace(grant.ItemId) &&
                    _catalog.TryGet(grant.ItemId, out _));
        }

        private LiveEventOperationResult TryPersistProfile()
        {
            if (_persistProfile == null)
            {
                return LiveEventOperationResult.Success;
            }

            try
            {
                _persistProfile.Invoke();
                return LiveEventOperationResult.Success;
            }
            catch (Exception)
            {
                return LiveEventOperationResult.PersistenceFailed;
            }
        }

        private static string BuildOperationId(string eventId, string category, string sourceId) =>
            $"live-event:{eventId}:{category}:{sourceId}";
    }
}
