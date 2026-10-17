using System.Linq;
using NUnit.Framework;
using NextXI.Core;
using NextXI.Core.Match;

namespace NextXI.Tests
{
    public class MatchRulesTests
    {
        private static MatchEngine Setup()
        {
            var m = TestFixtures.Engine();
            m.StartMatch();
            TestFixtures.ClearPitch(m);
            return m;
        }

        [Test]
        public void BallCrossingTheLineBetweenThePostsIsAGoal()
        {
            var m = Setup();
            var shooter = m.Teams[0].Players[9];
            int side = m.AttackDir(0);
            m.ForceOpenPlay(new Vec3(side * 45f, 0.5f, 0.5f), new Vec3(side * 20f, 0.5f, 0), shooter);
            TestFixtures.Run(m, 1.0f);
            Assert.AreEqual(1, m.Score(0));
            Assert.AreEqual(MatchPhase.GoalScored, m.Phase);
            var goal = m.Events.All.Single(e => e.Type == MatchEventType.Goal);
            Assert.AreEqual(0, goal.Team);
            Assert.AreEqual(shooter.Id, goal.PlayerId);
            Assert.AreEqual(1, shooter.Stats.Goals);
        }

        [Test]
        public void ShotWideOfThePostIsNotAGoalAndGivesGoalKick()
        {
            var m = Setup();
            var shooter = m.Teams[0].Players[9];
            int side = m.AttackDir(0);
            m.ForceOpenPlay(new Vec3(side * 45f, 0.5f, 6f), new Vec3(side * 20f, 0.5f, 0), shooter);
            TestFixtures.Run(m, 1.0f);
            Assert.AreEqual(0, m.Score(0));
            Assert.AreEqual(RestartType.GoalKick, m.CurrentRestart.Type);
            Assert.AreEqual(1, m.CurrentRestart.Team);
        }

        [Test]
        public void BallOverTheBarIsNotAGoal()
        {
            var m = Setup();
            var shooter = m.Teams[0].Players[9];
            int side = m.AttackDir(0);
            m.ForceOpenPlay(new Vec3(side * 48f, 3.2f, 0f), new Vec3(side * 15f, 1f, 0), shooter);
            TestFixtures.Run(m, 1.0f);
            Assert.AreEqual(0, m.Score(0));
        }

        [Test]
        public void DefenderLastTouchOverOwnGoalLineGivesCorner()
        {
            var m = Setup();
            var defender = m.Teams[1].Players[2];
            int side = m.AttackDir(0);
            m.ForceOpenPlay(new Vec3(side * 48f, 0.2f, 15f), new Vec3(side * 12f, 0, 0), defender);
            TestFixtures.Run(m, 1.0f);
            Assert.AreEqual(RestartType.Corner, m.CurrentRestart.Type);
            Assert.AreEqual(0, m.CurrentRestart.Team);
            Assert.AreEqual(1, m.Teams[0].Stats.Corners);
        }

        [Test]
        public void BallOverTouchlineGivesThrowInToOtherTeam()
        {
            var m = Setup();
            var p = m.Teams[0].Players[5];
            m.ForceOpenPlay(new Vec3(5f, 0.11f, 30f), new Vec3(0, 0, 10f), p);
            TestFixtures.Run(m, 1.0f);
            Assert.AreEqual(RestartType.ThrowIn, m.CurrentRestart.Type);
            Assert.AreEqual(1, m.CurrentRestart.Team);
            Assert.AreEqual(m.Pitch.HalfWidth, System.Math.Abs(m.CurrentRestart.Spot.Z), 0.01f);
        }

        [Test]
        public void ThrowInIsTakenAndPlayResumes()
        {
            var m = TestFixtures.Engine(seed: 3);
            m.StartMatch();
            TestFixtures.Run(m, 2f);
            var p = m.Teams[0].Players[5];
            m.ForceOpenPlay(new Vec3(5f, 0.11f, 33.9f), new Vec3(0, 0, 10f), p);
            TestFixtures.Run(m, 1.0f);
            Assert.AreEqual(RestartType.ThrowIn, m.CurrentRestart.Type);
            TestFixtures.Run(m, 12f);
            Assert.IsTrue(m.Events.All.Any(e => e.Type == MatchEventType.Pass && e.Team == 1 && e.VirtualTime > 0), "throw-in was never taken");
        }

        [Test]
        public void PassToPlayerInOffsidePositionIsPenalised()
        {
            var m = Setup();
            int dir = m.AttackDir(0);
            var passer = m.Teams[0].Players[6];
            var striker = m.Teams[0].Players[9];
            // Defenders of team 1 at 20 m, striker beyond them.
            int k = 0;
            foreach (var d in m.Teams[1].Players)
            {
                if (d.IsGoalkeeper) m.PlacePlayer(d, new Vec3(dir * 51f, 0, 0), 0);
                else m.PlacePlayer(d, new Vec3(dir * (20f + (k++ % 3)), 0, -25f + k * 4f), 0);
            }
            m.PlacePlayer(passer, new Vec3(dir * 5f, 0, 0), dir > 0 ? 0f : MathUtil.Pi);
            m.PlacePlayer(striker, new Vec3(dir * 32f, 0, 0), 0);
            m.ForceOpenPlay(new Vec3(dir * 5.4f, BallPhysics.Radius, 0), Vec3.Zero, passer);
            m.GiveBallTo(passer);
            Assert.IsTrue(m.StartKick(passer, new PendingKick { Kind = KickKind.GroundPass, Target = striker.Pos, ReceiverId = striker.Id, Power = 0.6f, ErrorScale = 0.01f }));
            TestFixtures.Run(m, 5f);
            Assert.IsTrue(m.Events.All.Any(e => e.Type == MatchEventType.Offside && e.PlayerId == striker.Id));
            Assert.AreEqual(RestartType.FreeKick, m.CurrentRestart.Type);
            Assert.AreEqual(1, m.CurrentRestart.Team);
        }

        [Test]
        public void OnsidePassIsCompleted()
        {
            var m = Setup();
            int dir = m.AttackDir(0);
            var passer = m.Teams[0].Players[6];
            var mate = m.Teams[0].Players[7];
            m.PlacePlayer(passer, new Vec3(-dir * 10f, 0, 0), dir > 0 ? 0f : MathUtil.Pi);
            m.PlacePlayer(mate, new Vec3(dir * 5f, 0, 0), 0);
            m.ForceOpenPlay(new Vec3(-dir * 9.6f, BallPhysics.Radius, 0), Vec3.Zero, passer);
            m.GiveBallTo(passer);
            m.StartKick(passer, new PendingKick { Kind = KickKind.GroundPass, Target = mate.Pos, ReceiverId = mate.Id, Power = 0.5f, ErrorScale = 0.01f });
            TestFixtures.Run(m, 4f);
            Assert.AreEqual(1, passer.Stats.Passes);
            Assert.AreEqual(1, passer.Stats.PassesCompleted);
            Assert.AreEqual(mate, m.Owner);
        }

        [Test]
        public void FoulInsideTheBoxGivesPenalty()
        {
            var m = Setup();
            int dir = m.AttackDir(0);
            var attacker = m.Teams[0].Players[9];
            var defender = m.Teams[1].Players[2];
            var spot = new Vec3(dir * 45f, 0, 0);
            m.ForceOpenPlay(new Vec3(spot.X, BallPhysics.Radius, 0), Vec3.Zero, attacker);
            m.CallFoul(defender, attacker, spot, false);
            Assert.AreEqual(RestartType.Penalty, m.CurrentRestart.Type);
            Assert.AreEqual(0, m.CurrentRestart.Team);
            Assert.AreEqual(m.Pitch.PenaltySpot(dir).X, m.CurrentRestart.Spot.X, 0.01f);
        }

        [Test]
        public void FoulOutsideTheBoxGivesFreeKick()
        {
            var m = Setup();
            var attacker = m.Teams[0].Players[9];
            var defender = m.Teams[1].Players[2];
            var spot = new Vec3(0, 0, 10);
            m.ForceOpenPlay(new Vec3(0, BallPhysics.Radius, 10), Vec3.Zero, attacker);
            m.CallFoul(defender, attacker, spot, false);
            Assert.AreEqual(RestartType.FreeKick, m.CurrentRestart.Type);
            Assert.AreEqual(1, m.Teams[1].Stats.Fouls);
        }

        [Test]
        public void SecondYellowSendsOff()
        {
            var m = Setup();
            var attacker = m.Teams[0].Players[9];
            var defender = m.Teams[1].Players[2];
            for (int i = 0; i < 40 && !defender.SentOff; i++)
            {
                m.ForceOpenPlay(new Vec3(0, BallPhysics.Radius, 10), Vec3.Zero, attacker);
                m.CallFoul(defender, attacker, new Vec3(0, 0, 10), true);
            }
            Assert.IsTrue(defender.SentOff);
            Assert.AreEqual(2, defender.Yellow);
            Assert.AreEqual(10, m.Teams[1].ActiveCount);
        }
    }
}
