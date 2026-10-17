using System.Linq;
using NUnit.Framework;
using NextXI.Core;
using NextXI.Core.Career;
using NextXI.Core.Data;
using NextXI.Core.Match;

namespace NextXI.Tests
{
    public class TrainingDrillTests
    {
        private static DrillRun Make(string drill, Position pos = Position.ST, ulong seed = 3)
        {
            var own = TestFixtures.Team(seed, "OWN", 72);
            var spar = TestFixtures.Team(seed + 1, "SPR", 70, "4-4-2");
            var athlete = own.StartingXI.First(p => p.Position == pos || pos == Position.GK && p.Position == Position.GK);
            return new DrillRun(drill, athlete, own, spar, seed);
        }

        [TestCase("finishing")]
        [TestCase("heading")]
        [TestCase("passing")]
        [TestCase("control")]
        [TestCase("positioning")]
        [TestCase("dribbling")]
        [TestCase("defending")]
        [TestCase("speed")]
        [TestCase("agility")]
        public void DrillCompletesWithValidScore(string drill)
        {
            var d = Make(drill, drill == "defending" ? Position.CB : Position.ST);
            float score = d.RunHeadless();
            Assert.IsTrue(d.Finished, "drill did not finish");
            Assert.That(score, Is.InRange(0f, 1f));
            Assert.AreEqual(d.Attempts, d.Attempt);
        }

        [Test]
        public void FinishingDrillProducesGoals()
        {
            int successes = 0;
            for (ulong seed = 1; seed <= 3; seed++)
            {
                var d = Make("finishing", Position.ST, seed);
                d.RunHeadless();
                successes += d.Successes;
            }
            Assert.Greater(successes, 0, "an AI striker never scored in 24 attempts");
        }

        [Test]
        public void SpeedDrillRewardsFasterAthletes()
        {
            var slow = Make("speed", Position.ST, 5);
            slow.User.A.Pace = 40; slow.User.A.Acceleration = 40;
            var fast = Make("speed", Position.ST, 5);
            fast.User.A.Pace = 95; fast.User.A.Acceleration = 95;
            Assert.Greater(fast.RunHeadless(), slow.RunHeadless());
        }

        [Test]
        public void OnlyParticipantsAreOnThePitch()
        {
            var d = Make("passing");
            Assert.AreEqual(6, d.Engine.Players.Count(p => p.IsActive));
        }
    }
}
