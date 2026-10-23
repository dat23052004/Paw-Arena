using System;

namespace PawArena.Core.Time
{
    /// <summary>
    /// Provides the current UTC time to domain services.
    /// </summary>
    public interface IClock
    {
        /// <summary>
        /// Gets the current UTC timestamp.
        /// </summary>
        DateTimeOffset UtcNow { get; }
    }
}
