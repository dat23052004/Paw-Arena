using System;

namespace PawArena.Core.Eggs
{
    /// <summary>Trusted hatch intent; price and currency are resolved from the catalog.</summary>
    public readonly struct EggHatchRequest
    {
        public EggHatchRequest(string requestId, string eggId, string tierId, string hatchOptionId)
        {
            RequestId = string.IsNullOrWhiteSpace(requestId)
                ? throw new ArgumentException("Request ID is required.", nameof(requestId))
                : requestId;
            EggId = string.IsNullOrWhiteSpace(eggId)
                ? throw new ArgumentException("Egg ID is required.", nameof(eggId))
                : eggId;
            TierId = string.IsNullOrWhiteSpace(tierId)
                ? throw new ArgumentException("Tier ID is required.", nameof(tierId))
                : tierId;
            HatchOptionId = string.IsNullOrWhiteSpace(hatchOptionId)
                ? throw new ArgumentException("Hatch option ID is required.", nameof(hatchOptionId))
                : hatchOptionId;
        }

        public string RequestId { get; }

        public string EggId { get; }

        public string TierId { get; }

        public string HatchOptionId { get; }
    }
}
