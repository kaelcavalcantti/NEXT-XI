using System.Linq;
using NUnit.Framework;
using NextXI.Core;
using NextXI.Core.Data;
using NextXI.Core.Match;

namespace NextXI.Tests
{
    public class UserRoleTests
    {
        [Test]
        public void UserControlledKeeperIsAssistedByKeeperAi()
        {
            int saves = 0;
            for (int seed = 0; seed < 12; seed++)
            {
                var m = TestFixtures.Engine(seed: (ulong)(seed + 60));
                m.Settings.ControlMode = ControlMode.PlayerLock;
                var gkProfile = m.Teams[1].Goalkeeper.Profile;
                m.SetUserTeam(1, gkProfile.Id);
                Assert.IsTrue(m.UserPlayer.IsGoalkeeper);
                m.StartMatch();
                TestFixtures.ClearPitch(m);
                m.AiEnabled = true;
                foreach (var p in m.Players) if (!p.IsGoalkeeper) p.OnPitch = false;
                int dir = m.AttackDir(0);
                var gk = m.UserPlayer;
                m.PlacePlayer(gk, new Vec3(dir * 51.5f, 0, 0), dir > 0 ? MathUtil.Pi : 0);
                var shooter = m.Teams[0].Players[9];
                shooter.OnPitch = true;
                m.PlacePlayer(shooter, new Vec3(dir * 34f, 0, 4f), dir > 0 ? 0 : MathUtil.Pi);
                m.ForceOpenPlay(new Vec3(dir * 34.4f, BallPhysics.Radius, 4f), Vec3.Zero, shooter);
                m.GiveBallTo(shooter);
                m.StartKick(shooter, new PendingKick { Kind = KickKind.Shot, IsShot = true, Target = new Vec3(dir * 52.8f, 0.5f, -3.2f), Power = 0.8f, ErrorScale = 0.01f });
                TestFixtures.Run(m, 3f);
                saves += gk.Stats.Saves;
            }
            Assert.Greater(saves, 0, "user keeper never dives");
        }

        [Test]
        public void UserKeeperDistributesWithButtons()
        {
            var m = TestFixtures.Engine(seed: 4);
            m.Settings.ControlMode = ControlMode.PlayerLock;
            m.SetUserTeam(0, m.Teams[0].Goalkeeper.Profile.Id);
            m.StartMatch();
            TestFixtures.Run(m, 2f);
            var gk = m.UserPlayer;
            int dir = m.AttackDir(0);
            m.PlacePlayer(gk, new Vec3(-dir * 48f, 0, 0), dir > 0 ? 0 : MathUtil.Pi);
            m.ForceOpenPlay(new Vec3(-dir * 46f, 1.2f, 0), new Vec3(-dir * 6f, 0, 0), m.Teams[1].Players[9]);
            TestFixtures.Run(m, 1.5f);
            Assert.AreEqual(gk, m.Holder, "keeper did not catch the ball");
            // Without input the keeper keeps the ball (the AI only steps in after a long wait).
            TestFixtures.Run(m, 1.0f);
            Assert.AreEqual(gk, m.Holder);
            m.Command.Pass = true; m.Command.Move = new Vec3(dir, 0, 0.4f);
            TestFixtures.Run(m, 1.5f);
            Assert.IsNull(m.Holder);
            Assert.IsTrue(m.Events.All.Any(e => e.Type == MatchEventType.Pass && e.PlayerId == gk.Id), "keeper did not distribute");
        }

        [Test]
        public void BenchedAthleteComesOnAtTheCoachMinute()
        {
            var home = TestFixtures.Team(31, "H");
            var away = TestFixtures.Team(32, "A", 72, "4-4-2");
            var athlete = home.Bench.First(p => p.Position == Position.ST || p.Position == Position.CAM || p.Position == Position.RW);
            var m = new MatchEngine(home, away, new MatchSettings { RealMinutesPerHalf = 3, Seed = 3, ControlMode = ControlMode.PlayerLock });
            m.SetUserTeam(0, athlete.Id);
            Assert.IsTrue(m.UserOnBench);
            m.UserSubInMinute = 55f;
            m.UserAutopilot = true;
            m.RunToEnd();
            Assert.IsFalse(m.UserOnBench, "athlete never came on");
            Assert.That(m.UserPlayer.Stats.VirtualMinutesPlayed, Is.InRange(5f, 40f));
            var report = MatchReport.FromEngine(m);
            var line = report.Line(athlete.Id);
            Assert.IsNotNull(line);
            Assert.IsFalse(line.Started);
        }
    }
}
