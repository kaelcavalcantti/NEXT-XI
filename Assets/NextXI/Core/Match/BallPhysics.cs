using System;

namespace NextXI.Core.Match
{
    [Serializable]
    public struct BallState
    {
        public Vec3 Position;
        public Vec3 Velocity;
        /// <summary>Angular velocity (rad/s). Drives Magnus curve and visual spin.</summary>
        public Vec3 Spin;
        /// <summary>True while the ball is held by a goalkeeper or a throw-in taker (physics frozen).</summary>
        public bool Held;
    }

    /// <summary>
    /// Deterministic ball model: gravity, quadratic air drag, Magnus lift, bounce with
    /// restitution/friction, rolling resistance, goal frame collisions and net capture.
    /// The model is intentionally analytic so kicks can be solved exactly (see KickSolver).
    /// </summary>
    public static class BallPhysics
    {
        public const float Radius = 0.11f;
        public const float Gravity = 9.81f;
        /// <summary>0.5 * rho * Cd * A / m  (rho 1.2, Cd 0.25, A = pi r^2, m 0.43 kg)</summary>
        public const float DragK = 0.0133f;
        public const float MagnusK = 0.0032f;
        public const float Restitution = 0.62f;
        public const float BounceFriction = 0.82f;
        /// <summary>Constant rolling deceleration (m/s^2).</summary>
        public const float RollDecel = 0.45f;
        /// <summary>Velocity proportional rolling damping (1/s).</summary>
        public const float RollDamping = 0.24f;
        public const float SpinDecay = 0.35f;
        public const float MaxSpeed = 45f;
        public const float PostRestitution = 0.55f;
        public const float NetDamping = 0.12f;

        public static bool IsOnGround(in BallState b) => b.Position.Y <= Radius + 0.02f && MathF.Abs(b.Velocity.Y) < 0.6f;

        /// <summary>Advances the ball. Returns true when the ball hit the woodwork during this step.</summary>
        public static bool Step(ref BallState b, float dt, PitchSpec pitch)
        {
            if (b.Held) { b.Velocity = Vec3.Zero; b.Spin = Vec3.Zero; return false; }
            bool hit = false;

            float speed = b.Velocity.Length;
            int substeps = speed * dt > 0.25f ? (int)MathF.Ceiling(speed * dt / 0.25f) : 1;
            if (substeps > 8) substeps = 8;
            float h = dt / substeps;
            for (int i = 0; i < substeps; i++) hit |= Integrate(ref b, h, pitch);

            if (!b.Position.IsFinite || !b.Velocity.IsFinite)
            {
                Log.Error("Ball", "Non-finite ball state detected, resetting to centre");
                b.Position = new Vec3(0, Radius, 0);
                b.Velocity = Vec3.Zero;
                b.Spin = Vec3.Zero;
            }
            return hit;
        }

        private static bool Integrate(ref BallState b, float h, PitchSpec pitch)
        {
            Vec3 v = b.Velocity;
            bool grounded = b.Position.Y <= Radius + 0.005f && MathF.Abs(v.Y) < 0.6f;

            if (grounded)
            {
                // Rolling: friction + damping in the plane, ball glued to the turf.
                Vec3 flat = v.FlatXZ;
                float s = flat.Length;
                if (s > 1e-4f)
                {
                    float decel = (RollDecel + RollDamping * s) * h;
                    float ns = MathF.Max(0f, s - decel);
                    flat = flat * (ns / s);
                    // Side spin bends rolling balls slightly.
                    Vec3 bend = Vec3.Cross(new Vec3(0, b.Spin.Y, 0), flat) * (MagnusK * 0.25f);
                    flat = flat + bend * h;
                }
                v = flat;
                b.Position = new Vec3(b.Position.X, Radius, b.Position.Z);
                // Rolling spin follows ground speed (visual), side spin decays fast.
                b.Spin = new Vec3(v.Z / Radius, b.Spin.Y * MathF.Max(0f, 1f - 3f * h), -v.X / Radius);
            }
            else
            {
                float s = v.Length;
                Vec3 drag = v * (-DragK * s);
                Vec3 magnus = Vec3.Cross(b.Spin, v) * MagnusK;
                Vec3 acc = new Vec3(0, -Gravity, 0) + drag + magnus;
                v = v + acc * h;
                b.Spin = b.Spin * MathF.Max(0f, 1f - SpinDecay * h);
            }

            v = v.ClampLength(MaxSpeed);
            Vec3 prev = b.Position;
            Vec3 p = b.Position + v * h;

            // Ground bounce
            if (p.Y < Radius)
            {
                p.Y = Radius;
                if (v.Y < -0.6f)
                {
                    v.Y = -v.Y * Restitution;
                    v.X *= BounceFriction;
                    v.Z *= BounceFriction;
                    b.Spin = b.Spin * 0.6f;
                }
                else v.Y = 0f;
            }

            b.Position = p;
            b.Velocity = v;
            bool hit = CollideGoalFrame(ref b, prev, pitch, -1);
            hit |= CollideGoalFrame(ref b, prev, pitch, +1);
            return hit;
        }

        private static bool CollideGoalFrame(ref BallState b, Vec3 prev, PitchSpec pitch, int side)
        {
            float lineX = side * pitch.HalfLength;
            if (MathF.Abs(b.Position.X - lineX) > pitch.GoalDepth + 1f) return false;

            float hw = pitch.HalfGoalWidth;
            float gh = pitch.GoalHeight;
            float r = Radius + pitch.PostRadius;

            // Posts (vertical segments) and crossbar (horizontal segment).
            bool hit = CollideSegment(ref b, new Vec3(lineX, 0, -hw), new Vec3(lineX, gh, -hw), r);
            hit |= CollideSegment(ref b, new Vec3(lineX, 0, hw), new Vec3(lineX, gh, hw), r);
            hit |= CollideSegment(ref b, new Vec3(lineX, gh, -hw), new Vec3(lineX, gh, hw), r);

            // Net box behind the line: back, sides and roof absorb the ball.
            float depthIn = (b.Position.X - lineX) * side; // > 0 behind the goal line
            bool wasInside = (prev.X - lineX) * side > 0 && MathF.Abs(prev.Z) < hw && prev.Y < gh;
            bool nowWithinMouth = MathF.Abs(b.Position.Z) < hw && b.Position.Y < gh;
            if (depthIn > 0 && (wasInside || nowWithinMouth))
            {
                Vec3 p = b.Position;
                Vec3 v = b.Velocity;
                float back = pitch.GoalDepth - Radius;
                if (depthIn > back) { p.X = lineX + side * back; if (v.X * side > 0) v.X = -v.X * NetDamping; v.Z *= 0.5f; v.Y *= 0.5f; }
                if (MathF.Abs(p.Z) > hw - Radius) { p.Z = MathF.Sign(p.Z) * (hw - Radius); if (v.Z * p.Z > 0) v.Z = -v.Z * NetDamping; v.X *= 0.6f; }
                if (p.Y > gh - Radius) { p.Y = gh - Radius; if (v.Y > 0) v.Y = -v.Y * NetDamping; v.X *= 0.6f; }
                b.Position = p;
                b.Velocity = v;
            }
            else if (depthIn > 0 && depthIn < pitch.GoalDepth)
            {
                // Outside the goal: side netting and roof stop balls entering from the side/top.
                Vec3 p = b.Position;
                Vec3 v = b.Velocity;
                bool prevOutsideSide = MathF.Abs(prev.Z) >= hw;
                if (prevOutsideSide && MathF.Abs(p.Z) < hw + Radius && p.Y < gh)
                {
                    p.Z = MathF.Sign(prev.Z) * (hw + Radius);
                    v.Z = -v.Z * NetDamping;
                    v.X *= 0.5f;
                }
                if (prev.Y >= gh && p.Y < gh + Radius && MathF.Abs(p.Z) < hw)
                {
                    p.Y = gh + Radius;
                    v.Y = MathF.Abs(v.Y) * 0.2f;
                    v.X *= 0.6f; v.Z *= 0.6f;
                }
                b.Position = p;
                b.Velocity = v;
            }
            return hit;
        }

        private static bool CollideSegment(ref BallState b, Vec3 a, Vec3 c, float r)
        {
            Vec3 ac = c - a;
            float t = MathUtil.Clamp01(Vec3.Dot(b.Position - a, ac) / ac.SqrLength);
            Vec3 closest = a + ac * t;
            Vec3 d = b.Position - closest;
            float dist = d.Length;
            if (dist >= r || dist < 1e-5f) return false;
            Vec3 n = d / dist;
            b.Position = closest + n * r;
            float vn = Vec3.Dot(b.Velocity, n);
            if (vn < 0)
            {
                Vec3 vt = b.Velocity - n * vn;
                b.Velocity = vt * 0.85f - n * (vn * PostRestitution);
                b.Spin = b.Spin * 0.5f;
                return true;
            }
            return false;
        }

        /// <summary>Predicts the ball position after `time` seconds without mutating the state.</summary>
        public static BallState Predict(BallState b, float time, PitchSpec pitch, float dt = 1f / 30f)
        {
            int steps = (int)MathF.Ceiling(time / dt);
            for (int i = 0; i < steps; i++) Step(ref b, MathF.Min(dt, time - i * dt), pitch);
            return b;
        }

        /// <summary>
        /// Analytic rolling distance for a ground ball (no bounce/air time), used by the kick solver.
        /// v(t) = (v0 + a/c) e^{-ct} - a/c.
        /// </summary>
        public static float RollingDistance(float v0, float vEnd)
        {
            float a = RollDecel, c = RollDamping;
            if (vEnd >= v0) return 0;
            float t = MathF.Log((v0 + a / c) / (vEnd + a / c)) / c;
            return (v0 + a / c) * (1f - MathF.Exp(-c * t)) / c - (a / c) * t;
        }

        public static float RollingTime(float v0, float vEnd)
        {
            float a = RollDecel, c = RollDamping;
            if (vEnd >= v0) return 0;
            return MathF.Log((v0 + a / c) / (vEnd + a / c)) / c;
        }
    }
}
