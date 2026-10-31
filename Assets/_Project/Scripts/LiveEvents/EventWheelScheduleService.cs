using System;

namespace PawArena.LiveEvents
{
    /// <summary>Resolves the Wheel definition active in the current Event window.</summary>
    public sealed class EventWheelScheduleService
    {
        private readonly LiveEventDefinition _definition;
        private readonly EventSchedulePolicy _schedule;

        public EventWheelScheduleService(
            LiveEventDefinition definition,
            EventSchedulePolicy schedule)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        }

        /// <summary>Gets the currently scheduled Wheel, or null outside its window.</summary>
        public LiveEventWheelDefinition ActiveWheel
        {
            get
            {
                for (var i = 0; i < _definition.Wheels.Count; i++)
                {
                    var wheel = _definition.Wheels[i];
                    if (wheel != null &&
                        _schedule.IsAvailable(wheel.StartHourOffset, wheel.EndHourOffset))
                    {
                        return wheel;
                    }
                }

                return null;
            }
        }

        /// <summary>Gets a stable fallback used only to initialize shared Free Spin state.</summary>
        public LiveEventWheelDefinition FirstWheel =>
            _definition.Wheels.Count > 0 ? _definition.Wheels[0] : null;
    }
}
