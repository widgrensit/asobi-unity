using System;
using NUnit.Framework;

namespace Asobi.Tests
{
    // Exercises the pure backoff math AsobiRealtime schedules reconnects
    // with. The reconnect loop itself lives in AsobiRealtime.cs, which
    // depends on UnityEngine.JsonUtility and can't be linked into this
    // headless .NET project - see README.md for what remains untestable
    // without Unity PlayMode.
    public class ReconnectPolicyTests
    {
        [Test]
        public void MaxAttemptsMatchesDartParity()
        {
            Assert.That(AsobiReconnectPolicy.MaxAttempts, Is.EqualTo(10));
        }

        [Test]
        public void BaseDelayIsOneSecond()
        {
            Assert.That(AsobiReconnectPolicy.BaseDelay, Is.EqualTo(TimeSpan.FromSeconds(1)));
        }

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 4)]
        [TestCase(3, 8)]
        [TestCase(4, 16)]
        [TestCase(5, 32)]
        [TestCase(6, 64)]
        [TestCase(7, 128)]
        [TestCase(8, 256)]
        [TestCase(9, 512)]
        public void GetDelayDoublesFromOneSecondBase(int attemptIndex, double expectedSeconds)
        {
            var delay = AsobiReconnectPolicy.GetDelay(attemptIndex);
            Assert.That(delay, Is.EqualTo(TimeSpan.FromSeconds(expectedSeconds)));
        }

        [Test]
        public void GetDelayRejectsNegativeAttemptIndex()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AsobiReconnectPolicy.GetDelay(-1));
        }

        [Test]
        public void DelayAtCapExceedsFiveMinutes()
        {
            var lastAttemptDelay = AsobiReconnectPolicy.GetDelay(AsobiReconnectPolicy.MaxAttempts - 1);
            Assert.That(lastAttemptDelay, Is.EqualTo(TimeSpan.FromSeconds(512)));
        }
    }
}
