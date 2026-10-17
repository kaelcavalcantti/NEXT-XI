using System;
using NUnit.Framework;
using NextXI.Core;
using NextXI.Core.Match;

namespace NextXI.Tests
{
    public class BallPhysicsTests
    {
        private readonly PitchSpec _pitch = new PitchSpec();

        [Test]
        public void BallAtRestStaysOnTheGround()
        {
            var b = new BallState { Position = new Vec3(0, BallPhysics.Radius, 0) };
            for (int i = 0; i < 600; i++) BallPhysics.Step(ref b, 1f / 60f, _pitch);
            Assert.AreEqual(BallPhysics.Radius, b.Position.Y, 1e-4);
            Assert.Less(b.Velocity.Length, 1e-3);
        }

        [Test]
        public void DroppedBallBouncesLowerEachTimeAndNeverSinks()
        {
            var b = new BallState { Position = new Vec3(0, 3f, 0) };
            float lastPeak = 3f, peak = 0f;
            bool rising = false;
            int bounces = 0;
            for (int i = 0; i < 60 * 8; i++)
            {
                BallPhysics.Step(ref b, 1f / 60f, _pitch);
                Assert.GreaterOrEqual(b.Position.Y, BallPhysics.Radius - 1e-4, "ball went below the turf");
                if (b.Velocity.Y > 0) { rising = true; peak = Math.Max(peak, b.Position.Y); }
                else if (rising) { rising = false; Assert.Less(peak, lastPeak); lastPeak = peak; peak = 0; bounces++; }
            }
            Assert.GreaterOrEqual(bounces, 3);
            Assert.AreEqual(BallPhysics.Radius, b.Position.Y, 0.01);
        }

        [Test]
        public void RollingBallDeceleratesAndStops()
        {
            var b = new BallState { Position = new Vec3(-40, BallPhysics.Radius, 0), Velocity = new Vec3(12, 0, 0) };
            float lastSpeed = 12f;
            for (int i = 0; i < 60 * 20; i++)
            {
                BallPhysics.Step(ref b, 1f / 60f, _pitch);
                float s = b.Velocity.Length;
                Assert.LessOrEqual(s, lastSpeed + 1e-4, "speed increased without a kick");
                lastSpeed = s;
            }
            Assert.Less(b.Velocity.Length, 0.01);
            // Analytic rolling distance must match the integrated one.
            float expected = BallPhysics.RollingDistance(12f, 0f);
            Assert.AreEqual(expected, b.Position.X + 40f, expected * 0.04f);
        }

        [Test]
        public void BallHittingThePostRebounds()
        {
            var pitch = _pitch;
            var b = new BallState { Position = new Vec3(pitch.HalfLength - 5f, 1f, pitch.HalfGoalWidth), Velocity = new Vec3(20, 0, 0) };
            bool hit = false;
            for (int i = 0; i < 60; i++) hit |= BallPhysics.Step(ref b, 1f / 60f, pitch);
            Assert.IsTrue(hit, "post collision not reported");
            Assert.Less(b.Velocity.X, 0f, "ball should bounce back off the post");
        }

        [Test]
        public void BallInTheNetIsContainedByTheNet()
        {
            var pitch = _pitch;
            var b = new BallState { Position = new Vec3(pitch.HalfLength - 3f, 1f, 0), Velocity = new Vec3(30, 0, 0) };
            for (int i = 0; i < 60 * 3; i++)
            {
                BallPhysics.Step(ref b, 1f / 60f, pitch);
                Assert.LessOrEqual(b.Position.X, pitch.HalfLength + pitch.GoalDepth + 0.01f, "ball passed through the back of the net");
            }
            Assert.Greater(b.Position.X, pitch.HalfLength);
        }

        [Test]
        public void SideSpinCurvesTheBall()
        {
            var straight = new BallState { Position = new Vec3(0, 0.2f, 0), Velocity = new Vec3(25, 4, 0) };
            var curled = straight;
            curled.Spin = new Vec3(0, 8f, 0);
            for (int i = 0; i < 45; i++) { BallPhysics.Step(ref straight, 1f / 60f, _pitch); BallPhysics.Step(ref curled, 1f / 60f, _pitch); }
            Assert.AreEqual(0f, straight.Position.Z, 1e-3);
            Assert.Greater(Math.Abs(curled.Position.Z), 0.1f);
        }
    }

    public class KickSolverTests
    {
        private readonly PitchSpec _pitch = new PitchSpec();

        [TestCase(10f, 6f)]
        [TestCase(25f, 7f)]
        [TestCase(40f, 4f)]
        public void GroundPassArrivesAtTarget(float distance, float arrival)
        {
            var from = new Vec3(0, BallPhysics.Radius, 0);
            var to = new Vec3(distance, 0, 0);
            var v = KickSolver.GroundPass(from, to, arrival);
            var b = new BallState { Position = from, Velocity = v };
            for (int i = 0; i < 60 * 15 && b.Position.X < distance; i++) BallPhysics.Step(ref b, 1f / 60f, _pitch);
            Assert.GreaterOrEqual(b.Position.X, distance - 0.3f, "pass fell short");
            Assert.AreEqual(arrival, b.Velocity.FlatLength, 0.6f);
        }

        [TestCase(30f, 1.6f)]
        [TestCase(50f, 2.7f)]
        public void LoftedPassLandsNearTarget(float distance, float flight)
        {
            var from = new Vec3(0, BallPhysics.Radius, 0);
            var to = new Vec3(distance, 0, 5f);
            var v = KickSolver.Lofted(from, to, flight, _pitch);
            var b = new BallState { Position = from, Velocity = v };
            bool airborne = false;
            float t = 0f;
            for (int i = 0; i < 60 * 6; i++)
            {
                float vyBefore = b.Velocity.Y;
                BallPhysics.Step(ref b, 1f / 60f, _pitch);
                t += 1f / 60f;
                if (b.Position.Y > 1f) airborne = true;
                // First ground contact: vertical speed flips upwards (bounce) or the ball settles.
                if (airborne && (vyBefore < 0f && b.Velocity.Y >= 0f)) break;
            }
            Assert.IsTrue(airborne);
            Assert.Less(Vec3.FlatDistance(b.Position, to), 1.5f);
            Assert.AreEqual(flight, t, 0.25f, "flight time");
            Assert.Less(v.Length, 33f, "unrealistic launch speed");
        }

        [Test]
        public void DrivenShotPassesAtRequestedHeight()
        {
            var from = new Vec3(30, BallPhysics.Radius, 0);
            var target = new Vec3(52.5f, 1.5f, 2f);
            var v = KickSolver.Driven(from, target, 26f, target.Y, _pitch);
            var b = new BallState { Position = from, Velocity = v };
            for (int i = 0; i < 120 && b.Position.X < 52.5f; i++) BallPhysics.Step(ref b, 1f / 120f, _pitch);
            Assert.AreEqual(1.5f, b.Position.Y, 0.3f);
            Assert.AreEqual(2f, b.Position.Z, 0.3f);
        }
    }
}
