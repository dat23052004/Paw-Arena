using NUnit.Framework;
using PawArena.Core.Match;

namespace PawArena.Tests.EditMode
{
    /// <summary>
    /// Verifies the GDD round lifecycle transition rules.
    /// </summary>
    public sealed class MatchStateMachineTests
    {
        [Test]
        public void TryTransitionTo_WhenFollowingFullRoundFlow_ReachesWaitingAgain()
        {
            var stateMachine = new MatchStateMachine();

            Assert.That(stateMachine.TryTransitionTo(MatchState.Countdown), Is.True);
            Assert.That(stateMachine.TryTransitionTo(MatchState.Spawning), Is.True);
            Assert.That(stateMachine.TryTransitionTo(MatchState.Playing), Is.True);
            Assert.That(stateMachine.TryTransitionTo(MatchState.Resolving), Is.True);
            Assert.That(stateMachine.TryTransitionTo(MatchState.Results), Is.True);
            Assert.That(stateMachine.TryTransitionTo(MatchState.Resetting), Is.True);
            Assert.That(stateMachine.TryTransitionTo(MatchState.Waiting), Is.True);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(MatchState.Waiting));
        }
    }
}
