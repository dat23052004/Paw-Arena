using NUnit.Framework;
using PawArena.Core.Economy;
using PawArena.Tests.EditMode.Fakes;

namespace PawArena.Tests.EditMode
{
    /// <summary>
    /// Verifies wallet balance, idempotency and insufficient-funds behavior.
    /// </summary>
    public sealed class WalletServiceTests
    {
        [Test]
        public void TryApply_WhenTransactionIsRepeated_AppliesOnlyOnce()
        {
            var wallet = new WalletService(new FakeClock());

            var first = wallet.TryApply(
                "daily-1",
                CurrencyType.Coins,
                50L,
                "daily_reward",
                "day_1",
                out _);
            var repeated = wallet.TryApply(
                "daily-1",
                CurrencyType.Coins,
                50L,
                "daily_reward",
                "day_1",
                out _);

            Assert.That(first, Is.EqualTo(WalletTransactionResult.Success));
            Assert.That(repeated, Is.EqualTo(WalletTransactionResult.DuplicateTransaction));
            Assert.That(wallet.GetBalance(CurrencyType.Coins), Is.EqualTo(50L));
        }

        [Test]
        public void TryApply_WhenDebitExceedsBalance_DoesNotMutateBalance()
        {
            var wallet = new WalletService(new FakeClock());

            var result = wallet.TryApply(
                "purchase-1",
                CurrencyType.Coins,
                -100L,
                "item_purchase",
                "fur_grey",
                out _);

            Assert.That(result, Is.EqualTo(WalletTransactionResult.InsufficientFunds));
            Assert.That(wallet.GetBalance(CurrencyType.Coins), Is.Zero);
        }
    }
}
