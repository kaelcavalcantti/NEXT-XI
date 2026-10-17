using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NextXI.Core;
using NextXI.Core.Match;

namespace NextXI.Tests
{
    public class MatchEngineTests
    {
        [Test]
        public void FullMatchRunsToFullTimeWithConsistentState()
        {
            var m = TestFixtures.Engine(3, seed: 11);
            m.StartMatch();
            int steps = 0;
            var lastPos = m.Players.ToDictionary(p => p.Id, p => p.Pos);
            var moved = m.Players.ToDictionary(p => p.Id, p => 0f);
            while (!m.IsFinished && steps < 400000)
            {
                if (m.Phase == MatchPhase.HalfTime) m.ContinueFromHalfTime();
                m.Step();
                steps++;
                Assert.IsTrue(m.Ball.Position.IsFinite && m.Ball.Velocity.IsFinite, "ball state became NaN");
                Assert.GreaterOrEqual(m.Ball.Position.Y, BallPhysics.Radius - 0.02f, "ball below the turf");
                Assert.Less(Math.Abs(m.Ball.Position.X), m.Pitch.HalfLength + 8f, "ball left the stadium (x)");
                Assert.Less(Math.Abs(m.Ball.Position.Z), m.Pitch.HalfWidth + 8f, "ball left the stadium (z)");
                if (steps % 60 == 0)
                    foreach (var p in m.Players)
                    {
                        Assert.IsTrue(p.Pos.IsFinite);
                        if (!lastPos.ContainsKey(p.Id)) { lastPos[p.Id] = p.Pos; moved[p.Id] = 0; }
                        moved[p.Id] += Vec3.FlatDistance(p.Pos, lastPos[p.Id]);
                        lastPos[p.Id] = p.Pos;
                    }
            }
            Assert.IsTrue(m.IsFinished, "match did not finish");
            Assert.AreEqual(MatchPeriod.FullTime, m.Clock.Period);
            // Events and scoreboard agree.
            for (int t = 0; t < 2; t++)
            {
                Assert.AreEqual(m.Score(t), m.Events.All.Count(e => e.Type == MatchEventType.Goal && e.Team == t));
                var s = m.Teams[t].Stats;
                Assert.GreaterOrEqual(s.Shots, s.ShotsOnTarget);
                Assert.GreaterOrEqual(s.Passes, s.PassesCompleted);
            }
            Assert.AreEqual(1, m.Events.All.Count(e => e.Type == MatchEventType.HalfTime));
            Assert.AreEqual(1, m.Events.All.Count(e => e.Type == MatchEventType.FullTime));
            // No outfield player stands still for the whole match.
            foreach (var p in m.Teams.SelectMany(t => t.Players).Where(p => !p.IsGoalkeeper))
                Assert.Greater(moved[p.Id], 150f, $"{p} barely moved");
        }

        [Test]
        public void PlayersDoNotOverlap()
        {
            var m = TestFixtures.Engine(3, seed: 5);
            m.StartMatch();
            int overlaps = 0, checks = 0;
            for (int i = 0; i < 60 * 90; i++)
            {
                m.Step();
                if (i % 10 != 0) continue;
                var act = m.Players.Where(p => p.IsActive && p.Action != PlayerAction.Slide && p.Action != PlayerAction.Fall && p.Action != PlayerAction.KeeperDive).ToList();
                for (int a = 0; a < act.Count; a++)
                    for (int b = a + 1; b < act.Count; b++)
                    {
                        checks++;
                        if (Vec3.FlatDistance(act[a].Pos, act[b].Pos) < 0.45f) overlaps++;
                    }
            }
            Assert.Less(overlaps / (float)checks, 0.002f);
        }

        [Test]
        public void MatchesProduceRealisticOutcomesAcrossSeeds()
        {
            int goals = 0, shots = 0, passes = 0, completed = 0;
            const int n = 8;
            for (int i = 0; i < n; i++)
            {
                var m = TestFixtures.Engine(3, seed: (ulong)(100 + i));
                m.RunToEnd();
                for (int t = 0; t < 2; t++)
                {
                    goals += m.Score(t);
                    shots += m.Teams[t].Stats.Shots;
                    passes += m.Teams[t].Stats.Passes;
                    completed += m.Teams[t].Stats.PassesCompleted;
                }
            }
            float gpm = goals / (float)n;
            Assert.That(gpm, Is.InRange(0.5f, 6f), "goals per match");
            Assert.Greater(shots / (float)n, 6f, "too few shots per match");
            Assert.That(completed / (float)passes, Is.InRange(0.55f, 0.95f), "pass completion");
        }

        [Test]
        public void StrongerTeamWinsMoreOften()
        {
            int strongPoints = 0, weakPoints = 0;
            for (int i = 0; i < 10; i++)
            {
                var m = TestFixtures.Engine(3, seed: (ulong)(500 + i), homeOverall: 84, awayOverall: 58);
                m.RunToEnd();
                int d = m.Score(0) - m.Score(1);
                strongPoints += d > 0 ? 3 : d == 0 ? 1 : 0;
                weakPoints += d < 0 ? 3 : d == 0 ? 1 : 0;
            }
            Assert.Greater(strongPoints, weakPoints);
        }

        [Test]
        public void SameSeedProducesSameMatch()
        {
            var a = TestFixtures.Engine(3, seed: 42);
            var b = TestFixtures.Engine(3, seed: 42);
            a.RunToEnd();
            b.RunToEnd();
            Assert.AreEqual(a.Score(0), b.Score(0));
            Assert.AreEqual(a.Score(1), b.Score(1));
            Assert.AreEqual(a.Events.All.Count, b.Events.All.Count);
            Assert.AreEqual(a.Ball.Position.X, b.Ball.Position.X);
        }

        [TestCase(3)]
        [TestCase(10)]
        public void MatchDurationOnlyChangesClockRate(int minutes)
        {
            var m = TestFixtures.Engine(minutes, seed: 9);
            m.StartMatch();
            int steps = 0;
            while (!m.IsFinished) { if (m.Phase == MatchPhase.HalfTime) m.ContinueFromHalfTime(); m.Step(); steps++; }
            float realSeconds = steps * MatchEngine.Dt;
            // Two halves of the chosen length plus stoppage (max 6 min virtual each) plus a few seconds of half-time.
            Assert.GreaterOrEqual(realSeconds, minutes * 120f);
            Assert.LessOrEqual(realSeconds, minutes * 120f * (1f + 2 * 7f / 45f) + 20f);
            foreach (var p in m.Teams.SelectMany(t => t.Players).Where(p => p.IsActive && p.Stats.VirtualMinutesPlayed > 80))
                Assert.That(p.Stats.VirtualMinutesPlayed, Is.InRange(88f, 104f));
        }

        [Test]
        public void SubstitutionReplacesPlayerAtNextStoppage()
        {
            var m = TestFixtures.Engine(3, seed: 2);
            m.StartMatch();
            var off = m.Teams[0].Players[7];
            Assert.IsTrue(m.RequestSubstitution(0, off.Id, 0));
            TestFixtures.Run(m, 1f);
            Assert.IsFalse(off.IsActive);
            Assert.AreEqual(11, m.Teams[0].ActiveCount);
            Assert.AreEqual(1, m.Teams[0].SubstitutionsUsed);
            Assert.IsTrue(m.Events.All.Any(e => e.Type == MatchEventType.Substitution));
            var on = m.Teams[0].Players[7];
            Assert.AreNotEqual(off, on);
            Assert.AreEqual(off.SlotIndex, on.SlotIndex);
        }

        [Test]
        public void SubstitutionLimitIsEnforced()
        {
            var m = TestFixtures.Engine(3, seed: 2);
            m.StartMatch();
            int ok = 0;
            for (int i = 1; i < 11; i++)
                if (m.RequestSubstitution(0, m.Teams[0].Players[i].Id, i - 1)) ok++;
            Assert.AreEqual(m.Settings.MaxSubstitutions, ok);
        }

        [Test]
        public void HigherPaceMeansHigherTopSpeed()
        {
            var m = TestFixtures.Engine();
            var p = m.Teams[0].Players[9];
            p.A.Pace = 40; float slow = p.MaxSpeed;
            p.A.Pace = 95; float fast = p.MaxSpeed;
            Assert.Greater(fast, slow + 1.5f);
            p.A.Acceleration = 30; float a1 = p.Acceleration;
            p.A.Acceleration = 95; float a2 = p.Acceleration;
            Assert.Greater(a2, a1);
        }

        [Test]
        public void PlayersAccelerateGraduallyAndCannotTurnInstantly()
        {
            var m = TestFixtures.Engine();
            m.StartMatch();
            TestFixtures.ClearPitch(m);
            m.ForceOpenPlay(new Vec3(0, BallPhysics.Radius, 30), Vec3.Zero);
            var p = m.Teams[0].Players[9];
            m.PlacePlayer(p, new Vec3(0, 0, 0), 0);
            p.Target = new Vec3(40, 0, 0); p.TargetSpeedFactor = 1f;
            m.Step();
            Assert.Less(p.Speed, 0.5f, "instant acceleration");
            TestFixtures.Run(m, 3f);
            Assert.Greater(p.Speed, p.MaxSpeed * 0.85f);
            Vec3 before = p.Vel;
            p.Target = p.Pos - new Vec3(40, 0, 0);
            m.Step();
            Assert.Greater(Vec3.Dot(p.Vel, before), 0f, "reversed direction in a single step");
        }

        [Test]
        public void UserShotFromCloseRangeCanScore()
        {
            int scored = 0;
            for (int seed = 0; seed < 10; seed++)
            {
                var m = TestFixtures.Engine(seed: (ulong)(seed + 20));
                m.SetUserTeam(0);
                m.StartMatch();
                TestFixtures.ClearPitch(m);
                int dir = m.AttackDir(0);
                var u = m.UserPlayer;
                m.PlacePlayer(u, new Vec3(dir * 41f, 0, 1f), dir > 0 ? 0 : MathUtil.Pi);
                m.ForceOpenPlay(new Vec3(dir * 41.4f, BallPhysics.Radius, 1f), Vec3.Zero, u);
                m.GiveBallTo(u);
                m.Command.Shoot = true;
                m.Command.Power = 0.5f;
                m.Command.Move = new Vec3(dir, 0, 0);
                TestFixtures.Run(m, 2.5f);
                Assert.AreEqual(1, u.Stats.Shots, "shot was not taken");
                if (m.Score(0) == 1) scored++;
            }
            Assert.GreaterOrEqual(scored, 7, "shots into an empty goal from 11 m should mostly score");
        }

        [Test]
        public void GoalkeeperSavesSomeShots()
        {
            int saves = 0, shots = 0;
            for (int seed = 0; seed < 12; seed++)
            {
                var m = TestFixtures.Engine(seed: (ulong)(seed + 60));
                m.StartMatch();
                TestFixtures.ClearPitch(m);
                m.AiEnabled = true;
                // Keep everyone but the keepers far away and frozen.
                foreach (var p in m.Players) if (!p.IsGoalkeeper) { p.OnPitch = false; }
                int dir = m.AttackDir(0);
                var gk = m.Teams[1].Goalkeeper;
                m.PlacePlayer(gk, new Vec3(dir * 51.5f, 0, 0), dir > 0 ? MathUtil.Pi : 0);
                var shooter = m.Teams[0].Players[9];
                shooter.OnPitch = true;
                m.PlacePlayer(shooter, new Vec3(dir * 34f, 0, 4f), dir > 0 ? 0 : MathUtil.Pi);
                m.ForceOpenPlay(new Vec3(dir * 34.4f, BallPhysics.Radius, 4f), Vec3.Zero, shooter);
                m.GiveBallTo(shooter);
                m.StartKick(shooter, new PendingKick { Kind = KickKind.Shot, IsShot = true, Target = new Vec3(dir * 52.8f, 0.5f, -3.2f), Power = 0.8f, ErrorScale = 0.01f });
                shots++;
                TestFixtures.Run(m, 3f);
                saves += gk.Stats.Saves;
            }
            Assert.Greater(saves, 0, "keeper never saves");
            Assert.Less(saves, shots, "keeper saves everything");
        }
    }
}
