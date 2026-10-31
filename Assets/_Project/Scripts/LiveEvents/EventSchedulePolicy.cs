using System;
using System.Globalization;
using PawArena.Core.Time;

namespace PawArena.LiveEvents
{
    /// <summary>Centralizes all UTC and end-exclusive live-event schedule decisions.</summary>
    public sealed class EventSchedulePolicy
    {
        private readonly LiveEventDefinition _definition;
        private readonly IClock _clock;
        private readonly DateTimeOffset _startsAt;
        private readonly DateTimeOffset _endsAt;

        /// <summary>Creates a schedule policy for one event definition.</summary>
        public EventSchedulePolicy(LiveEventDefinition definition, IClock clock)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _definition.TryGetSchedule(out _startsAt, out _endsAt);
        }

        /// <summary>Gets the configured UTC start.</summary>
        public DateTimeOffset StartsAt => _startsAt;

        /// <summary>Gets the configured exclusive UTC end.</summary>
        public DateTimeOffset EndsAt => _endsAt;

        /// <summary>Gets whether the configured schedule is valid.</summary>
        public bool HasValidSchedule => _endsAt > _startsAt;

        /// <summary>Gets whether the event is active now.</summary>
        public bool IsActive => HasValidSchedule && _clock.UtcNow >= _startsAt && _clock.UtcNow < _endsAt;

        /// <summary>Gets remaining event time, clamped to zero.</summary>
        public TimeSpan EventTimeRemaining => ClampRemaining(_endsAt - _clock.UtcNow);

        /// <summary>Gets remaining time until the next configured daily reset.</summary>
        public TimeSpan DailyResetTimeRemaining => ClampRemaining(GetNextDailyReset() - _clock.UtcNow);

        /// <summary>Gets the stable reset key for the current Event day.</summary>
        public string DailyResetKey
        {
            get
            {
                var shifted = _clock.UtcNow.AddHours(-_definition.DailyResetHourUtc);
                return shifted.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Checks an offset window using start-inclusive, end-exclusive semantics.</summary>
        public bool IsAvailable(int startHourOffset, int endHourOffset)
        {
            if (!IsActive || endHourOffset <= startHourOffset)
            {
                return false;
            }

            var start = _startsAt.AddHours(Math.Max(0, startHourOffset));
            var end = _startsAt.AddHours(Math.Max(startHourOffset + 1, endHourOffset));
            if (end > _endsAt)
            {
                end = _endsAt;
            }

            return _clock.UtcNow >= start && _clock.UtcNow < end;
        }

        /// <summary>Gets remaining time to an offset end, clamped by the Event end.</summary>
        public TimeSpan GetAvailabilityTimeRemaining(int endHourOffset)
        {
            var end = _startsAt.AddHours(Math.Max(0, endHourOffset));
            if (end > _endsAt)
            {
                end = _endsAt;
            }

            return ClampRemaining(end - _clock.UtcNow);
        }

        private DateTimeOffset GetNextDailyReset()
        {
            var now = _clock.UtcNow;
            var todayReset = new DateTimeOffset(
                now.Year,
                now.Month,
                now.Day,
                _definition.DailyResetHourUtc,
                0,
                0,
                TimeSpan.Zero);
            return todayReset > now ? todayReset : todayReset.AddDays(1);
        }

        private static TimeSpan ClampRemaining(TimeSpan remaining) =>
            remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
