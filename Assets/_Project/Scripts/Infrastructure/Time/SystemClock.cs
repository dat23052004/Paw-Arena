using System;
using PawArena.Core.Time;

namespace PawArena.Infrastructure.Time
{
    /// <summary>
    /// Supplies operating-system UTC time to domain services.
    /// </summary>
    public sealed class SystemClock : IClock
    {
        /// <inheritdoc />
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
