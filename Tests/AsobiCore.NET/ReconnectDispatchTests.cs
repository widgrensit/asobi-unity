using NUnit.Framework;

namespace Asobi.Tests
{
    // Pins the OnReconnecting/OnReconnectFailed wiring on AsobiDispatcher.
    // AsobiRealtime is the only thing that actually calls the protected
    // Raise* methods (from ScheduleReconnect/ReconnectAfterDelayAsync
    // after a real unexpected close), and it can't be linked into this
    // headless project - see README.md.
    public class ReconnectDispatchTests
    {
        class TestableDispatcher : AsobiDispatcher
        {
            public void FireReconnecting(int attempt, int maxAttempts) => RaiseReconnecting(attempt, maxAttempts);
            public void FireReconnectFailed() => RaiseReconnectFailed();
        }

        [Test]
        public void OnReconnectingFiresWithAttemptAndMax()
        {
            var dispatcher = new TestableDispatcher();
            int? gotAttempt = null;
            int? gotMax = null;
            dispatcher.OnReconnecting += (attempt, max) => { gotAttempt = attempt; gotMax = max; };

            dispatcher.FireReconnecting(3, 10);

            Assert.That(gotAttempt, Is.EqualTo(3));
            Assert.That(gotMax, Is.EqualTo(10));
        }

        [Test]
        public void OnReconnectFailedFiresWithNoSubscribers()
        {
            var dispatcher = new TestableDispatcher();
            Assert.DoesNotThrow(() => dispatcher.FireReconnectFailed());
        }

        [Test]
        public void OnReconnectFailedFiresSubscriber()
        {
            var dispatcher = new TestableDispatcher();
            var fired = false;
            dispatcher.OnReconnectFailed += () => fired = true;

            dispatcher.FireReconnectFailed();

            Assert.That(fired, Is.True);
        }
    }
}
