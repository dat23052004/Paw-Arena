using System;
using PawArena.Core.Time;

namespace PawArena.Tests.EditMode.Fakes
{
    /// <summary>
    /// Supplies deterministic time to domain tests.
    /// </summary>
    public sealed class FakeClock : IClock
    {
        /// <inheritdoc />
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
    }
}
