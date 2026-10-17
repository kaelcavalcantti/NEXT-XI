using NUnit.Framework;
using NextXI.Core.Match;

namespace NextXI.Tests
{
    public class MatchClockTests
    {
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(8)]
        [TestCase(10)]
        public void HalfReaches45MinutesAfterChosenRealDuration(int minutes)
        {
            var clock = new MatchClock(minutes * 60f, 0f);
            clock.StartHalf(MatchPeriod.FirstHalf);
            float dt = 1f / 60f;
            int steps = (int)(minutes * 60f / dt);
            for (int i = 0; i < steps - 2; i++) clock.Tick(dt);
            Assert.AreEqual(44, clock.DisplayMinute);
            for (int i = 0; i < 4; i++) clock.Tick(dt);
            Assert.AreEqual(45, clock.DisplayMinute);
            Assert.AreEqual(2700f / (minutes * 60f), clock.Rate, 1e-3);
        }

        [Test]
        public void StoppageTimeIsAnnouncedAndDisplayed()
        {
            var clock = new MatchClock(180f);
            clock.StartHalf(MatchPeriod.FirstHalf);
            clock.AddStoppage(120f);
            bool ended = false;
            float dt = 1f / 60f;
            int guard = 0;
            while (!ended && guard++ < 100000) ended = clock.Tick(dt);
            Assert.IsTrue(clock.StoppageAnnounced);
            Assert.AreEqual(3, clock.StoppageMinutes);
            StringAssert.StartsWith("45+", clock.Display);
        }

        [Test]
        public void SecondHalfStartsAt45()
        {
            var clock = new MatchClock(180f);
            clock.StartHalf(MatchPeriod.FirstHalf);
            clock.EnterHalfTime();
            clock.StartHalf(MatchPeriod.SecondHalf);
            Assert.AreEqual("45:00", clock.Display);
            Assert.AreEqual(2700f, clock.TotalVirtualSeconds, 1e-3);
        }
    }
}
