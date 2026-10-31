using System;
using PawArena.Core.Eggs;

namespace PawArena.Infrastructure.Time
{
    /// <summary>Process-local random source used by non-deterministic production hatches.</summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random _random = new();

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            }

            return _random.Next(exclusiveMax);
        }
    }
}
