using System;

namespace NextXI.Core.Match
{
    /// <summary>
    /// Computes launch velocities that make the ball reach a target under the same
    /// physics model used by BallPhysics. Kicks are therefore consistent with what the
    /// ball actually does afterwards (no "teleport" passes).
    /// </summary>
    public static class KickSolver
    {
        /// <summary>Ground pass: speed so the rolling ball reaches `distance` with `arrivalSpeed`.</summary>
        public static float GroundPassSpeed(float distance, float arrivalSpeed)
        {
            float lo = arrivalSpeed, hi = 40f;
            if (BallPhysics.RollingDistance(hi, arrivalSpeed) < distance) return hi;
            for (int i = 0; i < 40; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (BallPhysics.RollingDistance(mid, arrivalSpeed) < distance) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        public static Vec3 GroundPass(Vec3 from, Vec3 to, float arrivalSpeed)
        {
            Vec3 d = (to - from).FlatXZ;
            float speed = GroundPassSpeed(d.Length, arrivalSpeed);
            return d.Normalized * speed;
        }

        /// <summary>
        /// Lofted ball that lands at `to` after approximately `flightTime` seconds.
        /// Starts from the drag-free analytic solution and corrects iteratively by
        /// simulating the real model (drag + no spin).
        /// </summary>
        public static Vec3 Lofted(Vec3 from, Vec3 to, float flightTime, PitchSpec pitch, float targetHeight = BallPhysics.Radius)
        {
            float T = MathF.Max(0.3f, flightTime);
            Vec3 delta = to - from;
            Vec3 v = new Vec3(delta.X / T, (targetHeight - from.Y + 0.5f * BallPhysics.Gravity * T * T) / T, delta.Z / T);
            for (int iter = 0; iter < 6; iter++)
            {
                Vec3 landing = SimulateUntilHeight(from, v, targetHeight, T * 2.5f, pitch, out float tHit);
                Vec3 err = (to - landing).FlatXZ;
                if (err.Length < 0.15f) break;
                float tc = MathF.Max(0.3f, tHit);
                v = v + err * (1f / tc);
            }
            return v.ClampLength(BallPhysics.MaxSpeed);
        }

        /// <summary>
        /// Driven shot/pass with a given speed: finds the low elevation angle so that the
        /// ball passes over `to` (flat distance) at `targetHeight`.
        /// </summary>
        public static Vec3 Driven(Vec3 from, Vec3 to, float speed, float targetHeight, PitchSpec pitch)
        {
            Vec3 flat = (to - from).FlatXZ;
            float dist = flat.Length;
            Vec3 dir = flat.Normalized;
            if (dist < 0.5f) return dir * speed;
            float lo = -0.25f, hi = 0.9f; // radians
            for (int i = 0; i < 24; i++)
            {
                float mid = (lo + hi) * 0.5f;
                Vec3 v = dir * (speed * MathF.Cos(mid)) + new Vec3(0, speed * MathF.Sin(mid), 0);
                float h = HeightAtDistance(from, v, dist, pitch);
                if (h < targetHeight) lo = mid; else hi = mid;
            }
            float ang = (lo + hi) * 0.5f;
            return dir * (speed * MathF.Cos(ang)) + new Vec3(0, speed * MathF.Sin(ang), 0);
        }

        private static float HeightAtDistance(Vec3 from, Vec3 v, float dist, PitchSpec pitch)
        {
            var b = new BallState { Position = from, Velocity = v };
            const float dt = 1f / 120f;
            Vec3 start = from.FlatXZ;
            Vec3 lastP = from;
            for (int i = 0; i < 600; i++)
            {
                lastP = b.Position;
                FlightStep(ref b, dt);
                float d = (b.Position.FlatXZ - start).Length;
                if (d >= dist)
                {
                    float dPrev = (lastP.FlatXZ - start).Length;
                    float t = MathUtil.InverseLerp(dPrev, d, dist);
                    return MathUtil.Lerp(lastP.Y, b.Position.Y, t);
                }
                if (b.Velocity.FlatLength < 0.5f) return b.Position.Y;
            }
            return b.Position.Y;
        }

        private static Vec3 SimulateUntilHeight(Vec3 from, Vec3 v, float h, float maxT, PitchSpec pitch, out float tHit)
        {
            var b = new BallState { Position = from, Velocity = v };
            const float dt = 1f / 120f;
            float t = 0;
            Vec3 last = from;
            bool rising = true;
            while (t < maxT)
            {
                last = b.Position;
                FlightStep(ref b, dt);
                t += dt;
                if (b.Velocity.Y < 0) rising = false;
                if (!rising && b.Position.Y <= h)
                {
                    float k = MathUtil.InverseLerp(last.Y, b.Position.Y, h);
                    tHit = t;
                    return Vec3.Lerp(last, b.Position, k);
                }
            }
            tHit = t;
            return b.Position;
        }

        /// <summary>Pure flight integration (no ground, no frame) used by the solvers.</summary>
        private static void FlightStep(ref BallState b, float dt)
        {
            Vec3 v = b.Velocity;
            float s = v.Length;
            Vec3 acc = new Vec3(0, -BallPhysics.Gravity, 0) + v * (-BallPhysics.DragK * s) + Vec3.Cross(b.Spin, v) * BallPhysics.MagnusK;
            v = v + acc * dt;
            b.Velocity = v;
            b.Position = b.Position + v * dt;
        }

        /// <summary>Lead target for a pass to a moving receiver.</summary>
        public static Vec3 LeadTarget(Vec3 from, Vec3 receiverPos, Vec3 receiverVel, float ballSpeed, int iterations = 3)
        {
            Vec3 target = receiverPos;
            for (int i = 0; i < iterations; i++)
            {
                float d = Vec3.FlatDistance(from, target);
                float t = d / MathF.Max(4f, ballSpeed * 0.75f);
                target = receiverPos + receiverVel.FlatXZ * t;
            }
            return target;
        }
    }
}
