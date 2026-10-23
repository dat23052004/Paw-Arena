using NUnit.Framework;
using PawArena.Core.Flow;

namespace PawArena.Tests.EditMode
{
    /// <summary>
    /// Verifies the GDD-aligned top-level game-flow rules.
    /// </summary>
    public sealed class GameFlowStateMachineTests
    {
        [Test]
        public void TryTransitionTo_WhenTransitionIsValid_ChangesStateAndRaisesEvent()
        {
            var stateMachine = new GameFlowStateMachine();
            var eventCount = 0;
            var observedPrevious = GameFlowState.Results;
            var observedCurrent = GameFlowState.Results;
            stateMachine.StateChanged += (previous, current) =>
            {
                eventCount++;
                observedPrevious = previous;
                observedCurrent = current;
            };

            var didTransition = stateMachine.TryTransitionTo(GameFlowState.Lobby);

            Assert.That(didTransition, Is.True);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(GameFlowState.Lobby));
            Assert.That(eventCount, Is.EqualTo(1));
            Assert.That(observedPrevious, Is.EqualTo(GameFlowState.Boot));
            Assert.That(observedCurrent, Is.EqualTo(GameFlowState.Lobby));
        }

        [Test]
        public void TryTransitionTo_WhenTransitionIsInvalid_KeepsCurrentState()
        {
            var stateMachine = new GameFlowStateMachine(GameFlowState.Lobby);

            var didTransition = stateMachine.TryTransitionTo(GameFlowState.Results);

            Assert.That(didTransition, Is.False);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(GameFlowState.Lobby));
        }

        [Test]
        public void TryTransitionTo_WhenPlayerIsEliminated_EntersSpectating()
        {
            var stateMachine = new GameFlowStateMachine(GameFlowState.Playing);

            Assert.That(stateMachine.TryTransitionTo(GameFlowState.Spectating), Is.True);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(GameFlowState.Spectating));
        }
    }
}
