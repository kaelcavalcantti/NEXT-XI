using System;
using NextXI.Core.Data;

namespace NextXI.Core.Match
{
    internal sealed partial class MatchAi
    {
        private readonly float[] _diveCommittedAt = { -10f, -10f };

        private void KeeperThink(MatchPlayer gk, float dt)
        {
            int team = gk.Team;
            int side = -m.AttackDir(team);
            Vec3 goal = Pitch.GoalCentre(side);

            if (m.Holder == gk)
            {
                gk.HoldTimer -= dt;
                gk.Target = gk.Pos;
                gk.TargetSpeedFactor = 0.3f;
                gk.Facing = MathUtil.MoveTowardsAngle(gk.Facing, (side > 0 ? MathUtil.Pi : 0f), 6f * dt);
                if (gk.HoldTimer <= 0f && gk.Action == PlayerAction.None) Distribute(gk);
                return;
            }

            if (m.Owner == gk)
            {
                // Ball at the keeper's feet (back pass or goal kick): play it simply and quickly.
                if (gk.IsBusy) return;
                gk.HoldTimer += dt;
                if (gk.HoldTimer < 0.5f && m.NearestOpponentDistance(gk) > 6f) { gk.Target = gk.Pos; return; }
                Option best = new Option { Utility = float.MinValue };
                EvaluatePasses(gk, ref best, 0.1f);
                if (best.Name == null || best.Utility < 0.002f || m.NearestOpponentDistance(gk) < 4f)
                {
                    Vec3 t = new Vec3(goal.X - side * 50f, 0, m.Rng.Range(-20f, 20f));
                    best = new Option { Name = "clear", IsKick = true, Kick = new PendingKick { Kind = KickKind.Clearance, Target = Pitch.ClampToField(t, 4f), Power = 1f, ReceiverId = -1, ErrorScale = ErrorScale(gk) } };
                }
                m.StartKick(gk, best.Kick);
                gk.HoldTimer = 0f;
                return;
            }

            if (gk.IsBusy) return;

            // 1. Shot reaction.
            if (m.Owner == null && m.SimTime - _shotSeenTime[team] < 2.5f && m.Ball.Velocity.X * side > 6f)
            {
                float reaction = MathUtil.Attr(gk.A.GkReflexes, 0.3f, 0.12f) * (IsAiOpponentOfUser(gk) ? m.Settings.OpponentReactionDelay / 0.25f : 1f);
                if (m.SimTime - _shotSeenTime[team] >= reaction && m.SimTime - _diveCommittedAt[team] > 1.2f)
                {
                    if (PredictPlaneCrossing(gk.Pos.X, side, out Vec3 c, out float t))
                    {
                        float lateral = c.Z - gk.Pos.Z;
                        float diveMax = MathUtil.Attr(gk.A.GkDiving, 4.2f, 6.8f);
                        if (gk.Profile.HasTrait(Traits.ShotStopper)) diveMax *= 1.08f;
                        bool reachableStanding = MathF.Abs(lateral) < 0.5f && c.Y < 1.9f;
                        if (!reachableStanding && MathF.Abs(lateral) < 4.8f && t > 0.06f)
                        {
                            // Dive so that the body (arm reach ~0.9 m) arrives on the ball line in time.
                            float travel = MathF.Max(0f, MathF.Abs(lateral) - 0.85f);
                            float speed = MathF.Min(diveMax, MathF.Max(2.5f, travel / MathF.Max(0.1f, t - 0.04f)));
                            float jump = c.Y > 1.4f ? MathUtil.Attr(gk.A.Jumping, 2.3f, 3.4f) : (c.Y > 0.7f ? 1.4f : 0f);
                            Vec3 divePoint = new Vec3(gk.Pos.X - side * 0.25f, 0, gk.Pos.Z + MathF.Sign(lateral) * (travel + 0.15f));
                            m.StartDive(gk, divePoint, speed, 1.0f, jump);
                            _diveCommittedAt[team] = m.SimTime;
                            return;
                        }
                        gk.Target = new Vec3(gk.Pos.X, 0, MathUtil.Clamp(c.Z, -Pitch.HalfGoalWidth, Pitch.HalfGoalWidth));
                        gk.TargetSpeedFactor = 1f;
                        if (c.Y > 1.8f && gk.IsGrounded) Jump(gk);
                        return;
                    }
                }
                return; // still reacting: stay set
            }

            // 2. Loose ball in the box: rush out and claim it.
            Vec3 ball = m.Ball.Position;
            bool ballInBox = Pitch.IsInsidePenaltyArea(ball, side);
            if (m.Owner == null && ballInBox && _chaser[team] == gk)
            {
                gk.Target = _chasePoint[team];
                gk.TargetSpeedFactor = 1f;
                gk.Intent = "claim";
                if (_chasePoint[team].Y > 1.5f && gk.IsGrounded && Vec3.FlatDistance(gk.Pos, _chasePoint[team]) < 1.2f) Jump(gk);
                return;
            }
            // Through ball / 1v1: come off the line to narrow the angle.
            var carrier = m.Owner;
            if (carrier != null && carrier.Team != team)
            {
                float dGoal = Vec3.FlatDistance(carrier.Pos, goal);
                bool clear = dGoal < 20f && m.NearestOpponentDistance(carrier) > 3f;
                if (clear && ballInBox)
                {
                    Vec3 toBall = (carrier.Pos - goal).FlatNormalized;
                    gk.Target = goal + toBall * MathF.Min(dGoal - 2.5f, 8f);
                    gk.TargetSpeedFactor = 0.95f;
                    gk.Intent = "narrow";
                    if (Vec3.FlatDistance(gk.Pos, m.Ball.Position) < 1.4f && gk.CanAct && m.Rng.Chance(0.15f))
                        m.StartSlide(gk, (m.Ball.Position - gk.Pos).FlatNormalized);
                    return;
                }
            }

            // 3. Angle positioning on the ball-goal bisector.
            Vec3 toB = (ball - goal).FlatXZ;
            float dist = toB.Length;
            float depth = MathUtil.Clamp(dist * 0.09f, 0.6f, 3.5f);
            float ballAx = m.AttackX(team, ball);
            if (ballAx > 0f) depth = MathUtil.Lerp(3.5f, 12f, MathUtil.Clamp01(ballAx / 30f)) * MathUtil.Attr(gk.A.GkPositioning, 0.8f, 1.1f);
            if (gk.Profile.HasTrait(Traits.Sweeper)) depth *= 1.15f;
            Vec3 target = goal + toB.Normalized * depth;
            target.Z = MathUtil.Clamp(target.Z, -Pitch.HalfGoalWidth - 0.5f, Pitch.HalfGoalWidth + 0.5f);
            target = new Vec3(MathUtil.Clamp(target.X * side, HL - 16f, HL - 0.3f) * side, 0, target.Z);
            gk.Target = target;
            gk.TargetSpeedFactor = Vec3.FlatDistance(gk.Pos, target) > 3f ? 0.8f : 0.45f;
            gk.Jockeying = true;
            gk.Intent = "position";
        }

        private bool PredictPlaneCrossing(float planeX, int side, out Vec3 point, out float time)
        {
            var b = m.Ball;
            point = b.Position; time = 0f;
            const float dt = 1f / 60f;
            for (int i = 0; i < 150; i++)
            {
                Vec3 prev = b.Position;
                BallPhysics.Step(ref b, dt, Pitch);
                time += dt;
                if ((prev.X - planeX) * side < 0 && (b.Position.X - planeX) * side >= 0)
                {
                    float t = MathUtil.InverseLerp(prev.X * side, b.Position.X * side, planeX * side);
                    point = Vec3.Lerp(prev, b.Position, t);
                    return true;
                }
            }
            return false;
        }

        private void Distribute(MatchPlayer gk)
        {
            int dir = m.AttackDir(gk.Team);
            MatchPlayer best = null;
            float bestU = float.MinValue;
            foreach (var r in m.Teams[gk.Team].Players)
            {
                if (!r.IsActive || r == gk) continue;
                float d = Vec3.FlatDistance(gk.Pos, r.Pos);
                if (d < 8f || d > 38f) continue;
                float open = OpennessAt(r.Pos, gk.Team);
                float safe = LaneSafety(gk.Pos, r.Pos, gk.Team, 15f);
                float u = safe * MathUtil.Clamp01(open / 8f) + (r.Pos.X - gk.Pos.X) * dir * 0.004f;
                if (u > bestU) { bestU = u; best = r; }
            }
            PendingKick k;
            if (best != null && bestU > 0.45f && m.Rng.Chance(0.7f))
                k = new PendingKick { Kind = KickKind.KeeperThrow, Target = best.Pos + best.Vel * 0.5f, ReceiverId = best.Id, Power = 0.6f, ErrorScale = ErrorScale(gk) };
            else
            {
                MatchPlayer fw = null;
                float far = float.MinValue;
                foreach (var r in m.Teams[gk.Team].Players)
                {
                    if (!r.IsActive || r == gk) continue;
                    float ax = r.Pos.X * dir;
                    if (ax > far && ax < HL - 20f) { far = ax; fw = r; }
                }
                Vec3 t = fw != null ? fw.Pos : new Vec3(dir * 10f, 0, 0);
                k = new PendingKick { Kind = KickKind.KeeperKick, Target = t, ReceiverId = fw?.Id ?? -1, Power = 1f, ErrorScale = ErrorScale(gk) };
            }
            gk.Facing = (k.Target - gk.Pos).FlatNormalized.FlatAngle;
            m.KeeperRelease(gk, k);
        }
    }
}
