using System;
using System.Collections.Generic;
using NextXI.Core.Data;

namespace NextXI.Core.Match
{
    /// <summary>
    /// Team and player decision making. Each player re-evaluates its situation several
    /// times per second from distances, velocities, space, possession, tactical role,
    /// fatigue and attributes; continuous steering happens in the engine.
    /// </summary>
    internal sealed partial class MatchAi
    {
        private readonly MatchEngine m;
        private const int PathSamples = 36;
        private const float PathStep = 0.1f;
        private readonly Vec3[] _path = new Vec3[PathSamples];
        private readonly MatchPlayer[] _chaser = new MatchPlayer[2];
        private readonly Vec3[] _chasePoint = new Vec3[2];
        private readonly float[] _chaseTime = new float[2];
        private readonly MatchPlayer[] _presser = new MatchPlayer[2];
        private readonly MatchPlayer[] _cover = new MatchPlayer[2];
        private readonly float[] _shotSeenTime = { -10f, -10f };
        private int _lastShotVersion = -1;
        private float _setPieceDelay;

        public MatchAi(MatchEngine engine) { m = engine; }

        private PitchSpec Pitch => m.Pitch;
        private float HL => m.Pitch.HalfLength;
        private float HW => m.Pitch.HalfWidth;

        private bool IsAiOpponentOfUser(MatchPlayer p) => m.UserTeam >= 0 && p.Team != m.UserTeam;

        private float DecisionNoise(MatchPlayer p)
        {
            float baseNoise = IsAiOpponentOfUser(p) ? m.Settings.OpponentDecisionNoise : 0.2f;
            return baseNoise + (1f - p.A.Vision / 99f) * 0.12f;
        }

        private float ReactionDelay(MatchPlayer p)
        {
            float r = IsAiOpponentOfUser(p) ? m.Settings.OpponentReactionDelay : 0.24f;
            return r * MathUtil.Attr(p.A.Reactions, 1.25f, 0.8f);
        }

        private float ErrorScale(MatchPlayer p) => IsAiOpponentOfUser(p) ? m.Settings.OpponentExecutionError : 1f;

        // ================================================================== main update

        public void Update(float dt)
        {
            PredictBallPath();
            ComputeChasers();
            DetectShots();
            foreach (var p in m.Players)
            {
                if (!p.IsActive) continue;
                if (p.UserControlled && !m.UserAutopilot) continue;
                p.DecisionTimer -= dt;
                if (p.RunTimer > 0) p.RunTimer -= dt;
                p.WantsHeader = false;
                if (p.IsGoalkeeper) KeeperThink(p, dt);
                else if (m.Owner == p) CarrierThink(p, dt);
                else OffBallThink(p, dt);
            }
            // The user's own player still needs header intent when going for aerial balls.
            var u = m.UserPlayer;
            if (u != null && u.IsActive && !m.UserAutopilot)
            {
                u.WantsHeader = m.Owner == null && m.Ball.Position.Y > 1.3f && Vec3.FlatDistance(u.Pos, m.Ball.Position) < 2.5f;
                if (u.WantsHeader && u.IsGrounded && u.Action == PlayerAction.None && m.Ball.Position.Y > 1.8f && Vec3.FlatDistance(u.Pos, m.Ball.Position) < 1.6f)
                    Jump(u);
            }
        }

        private void PredictBallPath()
        {
            var b = m.Ball;
            for (int i = 0; i < PathSamples; i++)
            {
                for (int k = 0; k < 3; k++) BallPhysics.Step(ref b, PathStep / 3f, m.Pitch);
                _path[i] = b.Position;
            }
        }

        /// <summary>Time for p to reach a point, including reaction and a turning penalty.</summary>
        public float TimeToReach(MatchPlayer p, Vec3 point, float reachRadius = 0.6f)
        {
            Vec3 d = (point - p.Pos).FlatXZ;
            float dist = MathF.Max(0f, d.Length - reachRadius);
            float speed = p.Speed;
            float turn = 0f;
            if (speed > 1f && dist > 0.1f)
            {
                float cos = Vec3.Dot(p.Vel / speed, d.Normalized);
                turn = (1f - cos) * 0.22f * (speed / 6f);
            }
            float vmax = p.MaxSpeed * 0.93f;
            // Acceleration phase approximation.
            float accelT = MathF.Max(0f, (vmax - speed * MathF.Max(0f, 1f - turn)) / p.Acceleration);
            float accelD = 0.5f * (vmax + speed) * accelT;
            float t = dist <= accelD ? MathF.Sqrt(2f * dist / p.Acceleration) : accelT + (dist - accelD) / vmax;
            return t + turn + (p.Stun > 0 ? p.Stun : 0f);
        }

        private void ComputeChasers()
        {
            for (int t = 0; t < 2; t++) { _chaser[t] = null; _presser[t] = null; _cover[t] = null; _chaseTime[t] = 99f; }

            if (m.Holder != null) return;

            if (m.Owner != null)
            {
                int defTeam = 1 - m.Owner.Team;
                MatchPlayer best = null, second = null;
                float bt = 99f, st = 99f;
                foreach (var p in m.Teams[defTeam].Players)
                {
                    if (!p.IsActive || p.IsGoalkeeper || (p.UserControlled && !m.UserAutopilot)) continue;
                    float tt = TimeToReach(p, m.Owner.Pos + m.Owner.Vel * 0.3f, 1.2f);
                    if (tt < bt) { second = best; st = bt; best = p; bt = tt; }
                    else if (tt < st) { second = p; st = tt; }
                }
                _presser[defTeam] = best;
                _cover[defTeam] = second;
                return;
            }

            for (int t = 0; t < 2; t++)
            {
                foreach (var p in m.Teams[t].Players)
                {
                    if (!p.IsActive || (p.UserControlled && !m.UserAutopilot)) continue;
                    if (p.IsGoalkeeper && !Pitch.IsInsidePenaltyArea(m.Ball.Position, -m.AttackDir(t))) continue;
                    float headTop = 1.9f + MathUtil.Attr(p.A.Jumping, 0.3f, 0.75f);
                    float react = ReactionDelay(p);
                    for (int i = 0; i < PathSamples; i++)
                    {
                        float ts = (i + 1) * PathStep;
                        if (_path[i].Y > headTop && !p.IsGoalkeeper) continue;
                        float tr = TimeToReach(p, _path[i]) + react * 0.5f;
                        if (tr <= ts || i == PathSamples - 1)
                        {
                            float score = MathF.Max(tr, ts);
                            if (score < _chaseTime[t])
                            {
                                _chaseTime[t] = score;
                                _chaser[t] = p;
                                _chasePoint[t] = _path[i];
                            }
                            break;
                        }
                    }
                }
            }

            // The intended receiver of a pass is the chaser for its team unless someone is much quicker.
            if (m.PassInFlight)
            {
                var r = m.FindPlayer(m.PassReceiverId);
                if (r != null && r.IsActive && _chaser[r.Team] != r && !(r.UserControlled && !m.UserAutopilot))
                {
                    float rt = 99f;
                    Vec3 rp = _path[PathSamples - 1];
                    for (int i = 0; i < PathSamples; i++)
                    {
                        float ts = (i + 1) * PathStep;
                        float tr = TimeToReach(r, _path[i]);
                        if (tr <= ts) { rt = ts; rp = _path[i]; break; }
                    }
                    if (rt < _chaseTime[r.Team] + 0.6f)
                    {
                        _chaser[r.Team] = r;
                        _chasePoint[r.Team] = rp;
                        _chaseTime[r.Team] = rt;
                    }
                }
            }
        }

        private void DetectShots()
        {
            if (m.TrajectoryVersion == _lastShotVersion) return;
            _lastShotVersion = m.TrajectoryVersion;
            if (m.Owner != null || m.Holder != null) return;
            for (int t = 0; t < 2; t++)
            {
                int side = -m.AttackDir(t);
                if (m.Ball.Velocity.X * side < 6f) continue;
                if (m.PredictGoalLineCrossing(m.Ball, side, out var pt, out var time, false) &&
                    MathF.Abs(pt.Z) < Pitch.HalfGoalWidth + 1.2f && pt.Y < Pitch.GoalHeight + 0.8f && time < 2.2f)
                    _shotSeenTime[t] = m.SimTime;
            }
        }

        // ================================================================== tactical shape

        public Vec3 ShapeTarget(MatchPlayer p, bool inPossession, Vec3 ballPos)
        {
            var team = m.Teams[p.Team];
            var slot = team.Formation.Slots[p.SlotIndex];
            var tac = team.Tactics;
            int dir = m.AttackDir(p.Team);
            float bax = ballPos.X * dir;
            float bz = ballPos.Z;

            float defLine, length, halfW, shift;
            if (inPossession)
            {
                defLine = MathUtil.Clamp(bax - 30f + tac.LineHeight * 8f, -36f, 14f);
                length = 42f + tac.Directness * 6f;
                halfW = MathUtil.Lerp(22f, 31f, tac.Width);
                shift = 0.18f;
            }
            else
            {
                defLine = MathUtil.Clamp(bax - 22f + (tac.LineHeight - 0.5f) * 12f, -38f, 2f + tac.LineHeight * 12f);
                length = 28f + (1f - tac.Pressing) * -4f + 4f;
                halfW = 18.5f;
                shift = 0.42f;
            }

            float ax = defLine + slot.Depth * length;
            float z = -slot.Lateral * dir * halfW + bz * shift;

            if (inPossession)
            {
                float offsideLine = m.SecondLastDefenderAttackX(p.Team);
                if (slot.Role == TacticalRole.Striker || slot.Role == TacticalRole.Winger || slot.Role == TacticalRole.AttackingMid)
                    ax = MathF.Min(ax, MathF.Max(offsideLine, bax) - 0.8f);
                if (slot.Role == TacticalRole.FullBack && MathF.Sign(bz) == MathF.Sign(z) && bax > 5f) ax += 8f * tac.Width;
            }
            else
            {
                if (slot.Role == TacticalRole.Striker) ax = MathF.Max(ax, -8f);
                if (slot.Role == TacticalRole.Winger) ax = MathF.Max(ax, -22f);
                // Stay goal-side of the ball.
                if (slot.Role == TacticalRole.CentreBack || slot.Role == TacticalRole.FullBack || slot.Role == TacticalRole.DefensiveMid)
                    ax = MathF.Min(ax, MathF.Max(bax - 2f, -HL + 5f));
            }

            ax = MathUtil.Clamp(ax, -HL + 3f, HL - 3f);
            z = MathUtil.Clamp(z, -HW + 1.5f, HW - 1.5f);
            return new Vec3(ax * dir, 0, z);
        }

        // ================================================================== off-ball

        private void OffBallThink(MatchPlayer p, float dt)
        {
            int team = p.Team;
            bool inPossession = m.PossessionTeam == team;
            p.Jockeying = false;
            p.Shielding = false;

            // Loose ball: designated chaser goes for it.
            if (m.Owner == null && m.Holder == null && _chaser[team] == p)
            {
                Vec3 cp = _chasePoint[team];
                p.Target = cp;
                p.TargetSpeedFactor = 1f;
                p.Intent = "chase";
                float tToBall = _chaseTime[team];
                if (cp.Y > 1.5f) p.WantsHeader = true;
                // Jump for aerial balls that will arrive at head height soon.
                if (p.WantsHeader && p.IsGrounded && p.Action == PlayerAction.None && tToBall < 0.35f && m.Ball.Position.Y > 1.6f &&
                    Vec3.FlatDistance(p.Pos, cp) < 1.4f)
                    Jump(p);
                return;
            }

            if (p.DecisionTimer > 0f && p.Intent != "chase") return;
            p.DecisionTimer = MathUtil.Lerp(0.2f, 0.32f, m.Rng.Next());

            Vec3 ballPos = m.Holder != null ? m.Holder.Pos : (m.Owner != null ? m.Owner.Pos : m.Ball.Position);

            if (inPossession) AttackingMovement(p, ballPos);
            else DefensiveMovement(p, ballPos);
        }

        private void AttackingMovement(MatchPlayer p, Vec3 ballPos)
        {
            int team = p.Team;
            int dir = m.AttackDir(team);
            Vec3 shape = ShapeTarget(p, true, ballPos);
            var carrier = m.Owner != null && m.Owner.Team == team ? m.Owner : null;
            float bax = ballPos.X * dir;
            float offsideLine = m.SecondLastDefenderAttackX(team);
            p.Intent = "shape";

            // Forward runs in behind the defensive line.
            if (p.RunTimer > 0f)
            {
                p.Target = p.RunTarget;
                p.TargetSpeedFactor = 1f;
                p.Intent = "run";
                return;
            }
            bool runner = p.Role == TacticalRole.Striker || p.Role == TacticalRole.Winger || p.Role == TacticalRole.AttackingMid ||
                          (p.Role == TacticalRole.CentralMid && p.A.Positioning > 70);
            if (runner && carrier != null && bax > -10f && carrier.A.Vision > 45)
            {
                float pax = p.Pos.X * dir;
                bool onside = pax < offsideLine - 0.3f;
                float pressureOnCarrier = m.NearestOpponentDistance(carrier);
                float chance = 0.08f + p.A.Positioning / 700f + (p.Role == TacticalRole.Striker ? 0.08f : 0f);
                if (onside && pax > offsideLine - 12f && pressureOnCarrier > 2.5f && m.Rng.Chance(chance))
                {
                    float laneZ = MathUtil.Clamp(p.Pos.Z + m.Rng.Range(-6f, 6f), -HW + 4f, HW - 4f);
                    if (p.Role == TacticalRole.Striker) laneZ = MathUtil.Clamp(laneZ * 0.6f, -12f, 12f);
                    float runAx = MathF.Min(offsideLine + m.Rng.Range(8f, 16f), HL - 7f);
                    p.RunTarget = new Vec3(runAx * dir, 0, laneZ);
                    p.RunTimer = m.Rng.Range(2.0f, 3.0f);
                    p.Target = p.RunTarget;
                    p.TargetSpeedFactor = 1f;
                    p.Intent = "run";
                    return;
                }
            }

            // Support the carrier: the closest teammates look for open passing angles.
            Vec3 target = shape;
            if (carrier != null && IsAmongNearestSupporters(p, carrier, 3))
                target = BestSupportSpot(p, carrier, shape);

            // In the box when a cross is likely: attack the near/far post zones.
            if (carrier != null && carrier.Pos.X * dir > HL - 25f && MathF.Abs(carrier.Pos.Z) > 13f &&
                (p.Role == TacticalRole.Striker || p.Role == TacticalRole.Winger && MathF.Sign(p.Pos.Z) != MathF.Sign(carrier.Pos.Z) || p.Role == TacticalRole.AttackingMid))
            {
                float zSide = MathF.Sign(carrier.Pos.Z);
                float zt = p.Role == TacticalRole.Striker ? zSide * 2.5f : -zSide * 3.5f;
                if (p.Role == TacticalRole.AttackingMid) zt = 0f;
                float axt = p.Role == TacticalRole.AttackingMid ? HL - 13f : HL - 6.5f;
                target = new Vec3(axt * dir, 0, zt);
            }

            p.Target = Pitch.ClampToField(target, 1f);
            float dist = Vec3.FlatDistance(p.Pos, p.Target);
            p.TargetSpeedFactor = dist > 12f ? 0.9f : (dist > 4f ? 0.68f : 0.45f);
            if (p.UserControlled == false && p.Stamina < 0.45f) p.TargetSpeedFactor *= 0.85f;
        }

        private bool IsAmongNearestSupporters(MatchPlayer p, MatchPlayer carrier, int count)
        {
            float myD = Vec3.FlatDistance(p.Pos, carrier.Pos);
            int closer = 0;
            foreach (var o in m.Teams[p.Team].Players)
            {
                if (!o.IsActive || o == p || o == carrier || o.IsGoalkeeper) continue;
                if (Vec3.FlatDistance(o.Pos, carrier.Pos) < myD) closer++;
            }
            return closer < count && myD < 28f;
        }

        private Vec3 BestSupportSpot(MatchPlayer p, MatchPlayer carrier, Vec3 shape)
        {
            int dir = m.AttackDir(p.Team);
            Vec3 best = shape;
            float bestScore = float.MinValue;
            for (int i = 0; i < 9; i++)
            {
                Vec3 cand;
                if (i == 0) cand = shape;
                else
                {
                    float ang = (i - 1) * MathUtil.TwoPi / 8f;
                    cand = Vec3.Lerp(shape, p.Pos, 0.3f) + Vec3.FromAngle(ang) * 6f;
                }
                cand = Pitch.ClampToField(cand, 2f);
                float d = Vec3.FlatDistance(cand, carrier.Pos);
                if (d < 6f || d > 30f) continue;
                float open = MathF.Min(6f, OpennessAt(cand, p.Team));
                float lane = LaneSafety(carrier.Pos, cand, p.Team);
                float fwd = (cand.X - carrier.Pos.X) * dir;
                float drift = Vec3.FlatDistance(cand, shape);
                float offside = cand.X * dir > m.SecondLastDefenderAttackX(p.Team) - 0.5f ? -3f : 0f;
                float score = open * 0.5f + lane * 3f + fwd * 0.06f - drift * 0.12f + offside;
                if (score > bestScore) { bestScore = score; best = cand; }
            }
            return best;
        }

        private void DefensiveMovement(MatchPlayer p, Vec3 ballPos)
        {
            int team = p.Team;
            Vec3 ownGoal = m.OwnGoal(team);
            var carrier = m.Owner != null && m.Owner.Team != team ? m.Owner : null;

            if (carrier != null && _presser[team] == p)
            {
                PressCarrier(p, carrier, ownGoal);
                return;
            }
            if (carrier != null && _cover[team] == p && m.UserTeam == team && m.TeammatePressActive)
            {
                PressCarrier(p, carrier, ownGoal);
                return;
            }
            if (carrier != null && _cover[team] == p)
            {
                Vec3 toGoal = (ownGoal - carrier.Pos).FlatNormalized;
                p.Target = carrier.Pos + toGoal * 6.5f;
                p.TargetSpeedFactor = 0.85f;
                p.Intent = "cover";
                return;
            }

            // Loose-ball opponent's chaser of our team handled above; here: zonal marking.
            Vec3 zone = ShapeTarget(p, false, ballPos);
            Vec3 target = zone;
            MatchPlayer mark = null;
            float best = 10f;
            foreach (var o in m.Teams[1 - team].Players)
            {
                if (!o.IsActive || o.IsGoalkeeper || o == carrier) continue;
                float d = Vec3.FlatDistance(o.Pos, zone);
                if (d < best) { best = d; mark = o; }
            }
            if (mark != null)
            {
                Vec3 markPos = mark.Pos + (ownGoal - mark.Pos).FlatNormalized * 1.7f + mark.Vel * 0.25f;
                float w = p.Role == TacticalRole.CentreBack || p.Role == TacticalRole.FullBack ? 0.62f :
                          (p.Role == TacticalRole.DefensiveMid || p.Role == TacticalRole.CentralMid ? 0.5f : 0.2f);
                // Tighter marking near our goal.
                float danger = MathUtil.Clamp01(1f - Vec3.FlatDistance(mark.Pos, ownGoal) / 40f);
                w = MathUtil.Clamp01(w + danger * 0.3f);
                target = Vec3.Lerp(zone, markPos, w);
                p.Intent = "mark";
            }
            else p.Intent = "zone";

            // Read passes: if a pass is travelling, the defender nearest to its line steps in.
            if (m.PassInFlight && _chaser[team] == p) { target = _chasePoint[team]; p.TargetSpeedFactor = 1f; p.Intent = "intercept"; }

            p.Target = Pitch.ClampToField(target, 0.5f);
            float dist = Vec3.FlatDistance(p.Pos, p.Target);
            p.TargetSpeedFactor = dist > 10f ? 0.95f : (dist > 3f ? 0.75f : 0.5f);
            if (p.Intent == "intercept") p.TargetSpeedFactor = 1f;
        }

        private void PressCarrier(MatchPlayer p, MatchPlayer carrier, Vec3 ownGoal)
        {
            var tac = m.Teams[p.Team].Tactics;
            Vec3 toGoal = (ownGoal - carrier.Pos).FlatNormalized;
            float d = Vec3.FlatDistance(p.Pos, carrier.Pos);
            p.Intent = "press";
            // Contain goal-side first, then engage the ball (sooner for aggressive/pressing teams
            // or when the carrier stands still/shields).
            if (d < 3.5f) p.PressTime += MatchEngine.Dt * 4f; else p.PressTime = 0f;
            float patience = MathUtil.Lerp(1.6f, 0.5f, tac.Pressing) * MathUtil.Attr(p.A.Aggression, 1.3f, 0.7f);
            bool engage = p.PressTime > patience || carrier.Speed < 1.2f && p.PressTime > patience * 0.5f;
            Vec3 contain = carrier.Pos + carrier.Vel * 0.25f + toGoal * 1.2f;
            p.Target = engage ? m.Ball.Position + m.Ball.Velocity * 0.15f : contain;
            p.TargetSpeedFactor = d > 5f ? 1f : (engage ? 0.85f : 0.75f);
            p.Jockeying = d < 3.5f && !engage;

            if (!p.CanAct) return;
            float ballDist = Vec3.FlatDistance(p.FootPoint(), m.Ball.Position);
            if (ballDist < 1.1f && (engage || m.Rng.Chance(0.02f + tac.Pressing * 0.03f)))
            {
                m.StartTackle(p);
                p.PressTime = 0f;
                return;
            }
            // Slide when the carrier is escaping and we are side-on, rarely.
            float speedDiff = carrier.Speed - p.Speed;
            if (ballDist > 1.6f && ballDist < 3.0f && speedDiff > -1f && carrier.Speed > 4f && m.Rng.Chance(0.006f * (p.A.SlidingTackle / 70f)))
            {
                Vec3 dir = (m.Ball.Position + m.Ball.Velocity * 0.25f - p.Pos).FlatNormalized;
                if (Vec3.Dot(dir, carrier.FacingDir) > -0.2f) m.StartSlide(p, dir);
            }
        }

        // ================================================================== carrier

        private struct Option
        {
            public PendingKick Kick;
            public bool IsKick;
            public Vec3 DribbleTarget;
            public float Utility;
            public string Name;
        }

        private void CarrierThink(MatchPlayer p, float dt)
        {
            p.HoldTimer += dt;
            p.Jockeying = false;
            if (p.IsBusy) return;
            if (p.DecisionTimer > 0f)
            {
                SteerDribble(p);
                return;
            }
            p.DecisionTimer = MathUtil.Lerp(0.16f, 0.28f, m.Rng.Next()) + (IsAiOpponentOfUser(p) ? m.Settings.OpponentReactionDelay * 0.3f : 0f);

            float noise = DecisionNoise(p);
            float pressure = m.NearestOpponentDistance(p);
            Option best = EvaluateDribble(p);
            best.Utility += m.Rng.Gaussian() * noise * 0.04f;

            bool patient = p.HoldTimer < 0.35f && pressure > 2.5f;
            if (!patient)
            {
                Consider(ref best, EvaluateShot(p), noise);
                EvaluatePasses(p, ref best, noise);
                Consider(ref best, EvaluateCross(p), noise);
                Consider(ref best, EvaluateClearance(p, pressure), noise);
            }

            p.Shielding = !best.IsKick && pressure < 1.6f && best.Name == "hold";
            if (best.IsKick)
            {
                p.Intent = best.Name;
                if (m.StartKick(p, best.Kick)) { p.HoldTimer = 0f; return; }
            }
            p.Intent = best.Name;
            p.Target = best.DribbleTarget;
            SteerDribble(p);
        }

        private void Consider(ref Option best, Option o, float noise)
        {
            if (o.Name == null) return;
            o.Utility += m.Rng.Gaussian() * noise * 0.05f;
            if (o.Utility > best.Utility) best = o;
        }

        private void SteerDribble(MatchPlayer p)
        {
            Vec3 to = (p.Target - p.Pos).FlatXZ;
            if (to.SqrLength > 0.04f) p.DribbleDir = to.Normalized;
            float d = to.Length;
            p.TargetSpeedFactor = p.Shielding ? 0.3f : (d > 3f ? MathUtil.Lerp(0.75f, 1f, m.Teams[p.Team].Tactics.Directness) : 0.5f);
        }

        /// <summary>
        /// Positional value of having the ball at `pos` for `team`, in "goal probability" units
        /// (comparable with xG): ~0.01 in the own half, ~0.1-0.2 inside the box.
        /// </summary>
        public float PositionValue(int team, Vec3 pos)
        {
            float ax = m.AttackX(team, pos);
            float n = MathUtil.Clamp01((ax + HL) / (2f * HL));
            Vec3 goal = m.OpponentGoal(team);
            float dGoal = Vec3.FlatDistance(pos, goal);
            Vec3 p1 = new Vec3(goal.X, 0, -Pitch.HalfGoalWidth) - pos.FlatXZ;
            Vec3 p2 = new Vec3(goal.X, 0, Pitch.HalfGoalWidth) - pos.FlatXZ;
            float angle = MathF.Abs(Vec3.SignedFlatAngle(p1, p2));
            float angleFactor = MathUtil.Clamp(angle / 0.55f, 0.15f, 1f);
            return 0.005f + 0.03f * n * n + 0.3f * MathF.Exp(-dGoal / 9f) * angleFactor;
        }

        /// <summary>Expected cost of losing the ball at `pos`: the opponent's value there.</summary>
        private float LossCost(int team, Vec3 pos) => 0.01f + PositionValue(team, pos) * 0.5f + PositionValue(1 - team, pos) * 0.8f;

        private float OpennessAt(Vec3 pos, int team)
        {
            float best = 99f;
            foreach (var o in m.Teams[1 - team].Players)
            {
                if (!o.IsActive) continue;
                float d = Vec3.FlatDistance(o.Pos, pos);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Probability (0..1) that a ground ball from a to b is not intercepted.</summary>
        private float LaneSafety(Vec3 from, Vec3 to, int team, float ballSpeed = 14f)
        {
            float d = Vec3.FlatDistance(from, to);
            float vb = MathF.Max(5f, ballSpeed * 0.8f);
            float safe = 1f;
            foreach (var o in m.Teams[1 - team].Players)
            {
                if (!o.IsActive) continue;
                float t = MathUtil.ClosestFlatT(from, to, o.Pos);
                Vec3 c = Vec3.Lerp(from, to, t);
                float perp = Vec3.FlatDistance(c, o.Pos);
                float ballT = t * d / vb + 0.15f;
                float oppT = MathF.Max(0f, perp - 0.85f) / (o.MaxSpeed * 0.85f) + 0.28f;
                if (oppT < ballT)
                {
                    float risk = MathUtil.Clamp01((ballT - oppT) / 0.55f) * MathUtil.Attr(o.A.Interceptions, 0.5f, 0.92f);
                    if (o.IsGoalkeeper && !Pitch.IsInsidePenaltyArea(c, -m.AttackDir(o.Team))) risk *= 0.3f;
                    safe *= 1f - risk;
                }
            }
            return safe;
        }

        private Option EvaluateDribble(MatchPlayer p)
        {
            int dir = m.AttackDir(p.Team);
            Vec3 forward = new Vec3(dir, 0, 0);
            float holdPressure = m.NearestOpponentDistance(p);
            float holdU = PositionValue(p.Team, p.Pos) * (holdPressure < 2f ? 0.6f + 0.2f * holdPressure : 1f) - 0.004f * p.HoldTimer
                          - (holdPressure < 2f ? 0.01f : 0f);
            Option best = new Option { Name = "hold", DribbleTarget = p.Pos, Utility = holdU };
            float lossCost = LossCost(p.Team, p.Pos);
            float dribbleSkill = MathUtil.Attr(p.A.Dribbling, 0.4f, 1f);
            float[] angles = { 0f, 0.5f, -0.5f, 1.0f, -1.0f, 1.6f, -1.6f };
            foreach (float a in angles)
            {
                Vec3 d = forward.RotateY(a);
                Vec3 tgt = p.Pos + d * 5f;
                if (!Pitch.IsInsideField(Pitch.ClampToField(tgt, 2f)) || MathF.Abs(tgt.Z) > HW - 1.5f || MathF.Abs(tgt.X) > HL - 1f) continue;
                float risk = 0f;
                foreach (var o in m.Teams[1 - p.Team].Players)
                {
                    if (!o.IsActive) continue;
                    Vec3 rel = (o.Pos - p.Pos).FlatXZ;
                    float along = Vec3.Dot(rel, d);
                    float perp = (rel - d * along).Length;
                    if (along < -1f || along > 9f) continue;
                    float r = MathUtil.Clamp01((3.2f - perp) / 3.2f) * MathUtil.Clamp01((9f - along) / 9f);
                    risk = 1f - (1f - risk) * (1f - r * 0.75f);
                }
                float success = MathUtil.Clamp01(1f - risk * (1.25f - dribbleSkill * 0.6f));
                float u = success * PositionValue(p.Team, tgt) - (1f - success) * lossCost;
                if (u > best.Utility)
                {
                    best.Utility = u;
                    best.DribbleTarget = tgt;
                    best.Name = "dribble";
                }
            }
            if (p.IsGoalkeeper) best.Utility -= 0.03f;
            return best;
        }

        private Option EvaluateShot(MatchPlayer p)
        {
            int side = m.AttackDir(p.Team);
            float dist = Vec3.FlatDistance(p.Pos, m.OpponentGoal(p.Team));
            if (dist > 32f || p.IsGoalkeeper) return default;
            float xg = ShotModel.ExpectedGoals(m, p, m.Ball.Position, false);
            if (dist > 20f && p.A.LongShots < 60) xg *= 0.6f;
            var kick = BuildShot(p, MathUtil.Clamp01(0.55f + dist / 60f + m.Rng.Range(-0.08f, 0.1f)), 0f);
            float u = xg * 1.1f + (xg > 0.2f ? 0.05f : 0f);
            return new Option { Name = "shoot", IsKick = true, Kick = kick, Utility = u };
        }

        /// <summary>Builds an AI shot aimed at the corner furthest from the keeper.</summary>
        public PendingKick BuildShot(MatchPlayer p, float power, float aimBias)
        {
            int side = m.AttackDir(p.Team);
            var gk = m.Teams[1 - p.Team].Goalkeeper;
            float gkZ = gk != null ? gk.Pos.Z : 0f;
            float hw = Pitch.HalfGoalWidth;
            float lineZ = p.Pos.Z * 0.15f;
            float sign = gkZ > lineZ ? -1f : 1f;
            if (MathF.Abs(gkZ - lineZ) < 0.3f) sign = m.Rng.Chance(0.5f) ? 1f : -1f;
            if (aimBias != 0f) sign = MathF.Sign(aimBias);
            float z = sign * (hw - m.Rng.Range(0.45f, 0.9f));
            float y = m.Rng.Range(0.25f, power > 0.8f ? 1.9f : 1.3f);
            bool finesse = p.Profile.HasTrait(Traits.FinesseShot) && Vec3.FlatDistance(p.Pos, m.OpponentGoal(p.Team)) > 12f;
            return new PendingKick
            {
                Kind = KickKind.Shot,
                Target = new Vec3(side * (HL + 0.3f), y, z),
                Power = finesse ? MathF.Min(power, 0.7f) : power,
                IsShot = true,
                ReceiverId = -1,
                ErrorScale = ErrorScale(p),
                Curl = finesse ? -sign * side : 0f
            };
        }

        private void EvaluatePasses(MatchPlayer p, ref Option best, float noise)
        {
            int team = p.Team;
            int dir = m.AttackDir(team);
            float lossCost = LossCost(team, p.Pos);
            float offsideLine = m.SecondLastDefenderAttackX(team);
            float hereValue = PositionValue(team, p.Pos);
            foreach (var r in m.Teams[team].Players)
            {
                if (!r.IsActive || r == p) continue;
                float d = Vec3.FlatDistance(p.Pos, r.Pos);
                if (d < 4f || d > 55f) continue;
                bool offside = m.Settings.OffsideEnabled && r.Pos.X * dir > offsideLine + 0.1f && r.Pos.X * dir > 0 && r.Pos.X * dir > m.Ball.Position.X * dir;
                float offsideMult = offside ? 0.08f : 1f;
                float callBonus = r.UserControlled && r.CallForPassTimer > 0f ? 0.03f : 0f;

                // Pass to feet.
                bool lofted = d > 32f;
                Vec3 target = r.Pos + r.Vel * 0.6f;
                float success;
                if (!lofted)
                {
                    float arrival = 7f;
                    float speed = KickSolver.GroundPassSpeed(d, arrival);
                    success = LaneSafety(p.Pos, target, team, speed);
                }
                else
                {
                    float flight = MathUtil.Clamp(0.75f + d / 26f, 1.0f, 2.7f);
                    float oppT = 99f;
                    foreach (var o in m.Teams[1 - team].Players)
                        if (o.IsActive) oppT = MathF.Min(oppT, TimeToReach(o, target, 1f));
                    float recT = TimeToReach(r, target, 0.6f);
                    success = MathUtil.Clamp01(0.5f + (oppT - MathF.Max(recT, flight)) * 0.6f) * MathUtil.Attr(p.A.LongPassing, 0.6f, 0.95f);
                }
                float receiverSpace = MathUtil.Clamp01(OpennessAt(target, team) / 5f);
                success *= 0.7f + 0.3f * receiverSpace;
                if (r.IsGoalkeeper) success *= 0.9f;
                float value = PositionValue(team, target) * (0.85f + 0.3f * receiverSpace);
                // Small tempo bonus for forward progress; square/backwards passes need a reason.
                float progress = (target.X - p.Pos.X) * dir;
                value += MathUtil.Clamp(progress / 2000f, -0.004f, 0.006f);
                float u = (success * value - (1f - success) * lossCost) * offsideMult + callBonus * success;
                if (r.IsGoalkeeper && m.NearestOpponentDistance(p) > 3f) u -= 0.01f;
                // Avoid pointless sideways passing when the current spot is better.
                if (value < hereValue * 0.9f && m.NearestOpponentDistance(p) > 4f) u -= 0.003f;
                if (u > best.Utility)
                {
                    best = new Option
                    {
                        Name = lofted ? "longpass" : "pass",
                        IsKick = true,
                        Utility = u,
                        Kick = new PendingKick
                        {
                            Kind = lofted ? KickKind.Lofted : KickKind.GroundPass,
                            Target = target,
                            ReceiverId = r.Id,
                            Power = MathUtil.Clamp01(0.35f + d / 60f),
                            ErrorScale = ErrorScale(p)
                        }
                    };
                }

                // Through ball into the run.
                bool running = r.RunTimer > 0f || (r.Vel.X * dir > 4f && r.Role != TacticalRole.Goalkeeper);
                if (running && !offside)
                {
                    Vec3 lead = r.Pos + r.Vel * 1.1f + new Vec3(dir * 3.5f, 0, 0);
                    lead = Pitch.ClampToField(lead, 3f);
                    float dl = Vec3.FlatDistance(p.Pos, lead);
                    if (dl < 8f || dl > 45f) continue;
                    float speedL = KickSolver.GroundPassSpeed(dl, 4f);
                    float safe = LaneSafety(p.Pos, lead, team, speedL);
                    float recT = TimeToReach(r, lead, 0.7f);
                    float oppT = 99f;
                    foreach (var o in m.Teams[1 - team].Players)
                        if (o.IsActive) oppT = MathF.Min(oppT, TimeToReach(o, lead, 0.8f));
                    float race = MathUtil.Clamp01(0.5f + (oppT - recT) * 0.8f);
                    float s = safe * race * MathUtil.Attr(p.A.Vision, 0.55f, 1f);
                    float ut = s * (PositionValue(team, lead) * 1.15f + 0.005f) - (1f - s) * lossCost + (r.UserControlled && r.CallForThroughTimer > 0f ? 0.03f * s : 0f);
                    if (ut > best.Utility)
                    {
                        best = new Option
                        {
                            Name = "through",
                            IsKick = true,
                            Utility = ut,
                            Kick = new PendingKick { Kind = KickKind.ThroughPass, Target = lead, ReceiverId = r.Id, Power = 0.5f, ErrorScale = ErrorScale(p) }
                        };
                    }
                }
            }
        }

        private Option EvaluateCross(MatchPlayer p)
        {
            int dir = m.AttackDir(p.Team);
            float ax = p.Pos.X * dir;
            if (ax < HL - 26f || MathF.Abs(p.Pos.Z) < (ax > HL - 12f ? 8f : 12f) || p.IsGoalkeeper) return default;
            float zSide = MathF.Sign(p.Pos.Z);
            Vec3[] spots =
            {
                new Vec3((HL - 6f) * dir, 0, zSide * 2.5f),
                new Vec3((HL - 10.5f) * dir, 0, 0f),
                new Vec3((HL - 6.5f) * dir, 0, -zSide * 3.5f)
            };
            Option best = default;
            best.Utility = float.MinValue;
            foreach (var s in spots)
            {
                MatchPlayer bestMate = null;
                float mateT = 99f;
                foreach (var r in m.Teams[p.Team].Players)
                {
                    if (!r.IsActive || r == p || r.IsGoalkeeper) continue;
                    float t = TimeToReach(r, s, 1f);
                    if (t < mateT) { mateT = t; bestMate = r; }
                }
                if (bestMate == null) continue;
                float defT = 99f;
                foreach (var o in m.Teams[1 - p.Team].Players)
                    if (o.IsActive) defT = MathF.Min(defT, TimeToReach(o, s, 1f) + (o.IsGoalkeeper ? 0.3f : 0f));
                float flight = 1.3f;
                float win = MathUtil.Clamp01(0.45f + (defT - MathF.Max(mateT, flight)) * 0.5f) *
                            MathUtil.Attr(bestMate.A.Heading, 0.6f, 1f);
                float u = win * 0.13f * MathUtil.Attr(p.A.Crossing, 0.6f, 1.05f) + 0.004f;
                if (u > best.Utility)
                {
                    best = new Option
                    {
                        Name = "cross",
                        IsKick = true,
                        Utility = u,
                        Kick = new PendingKick { Kind = KickKind.Cross, Target = s, ReceiverId = bestMate.Id, Power = 0.6f, ErrorScale = ErrorScale(p), Curl = -zSide * dir * 0.5f }
                    };
                }
            }
            return best.Name == null ? default : best;
        }

        private Option EvaluateClearance(MatchPlayer p, float pressure)
        {
            int dir = m.AttackDir(p.Team);
            float ax = p.Pos.X * dir;
            if (ax > -HL + 24f || pressure > 2.2f) return default;
            Vec3 target = new Vec3((ax + 45f) * dir, 0, MathF.Sign(p.Pos.Z == 0 ? 1 : p.Pos.Z) * m.Rng.Range(12f, 26f));
            return new Option
            {
                Name = "clear",
                IsKick = true,
                Utility = 0.01f + (2.2f - pressure) * 0.015f,
                Kick = new PendingKick { Kind = KickKind.Clearance, Target = Pitch.ClampToField(target, 3f), ReceiverId = -1, Power = 1f, ErrorScale = ErrorScale(p) }
            };
        }

        // ================================================================== first-time actions

        public bool TryFirstTimeAction(MatchPlayer p, Vec3 contact, out PendingKick kick)
        {
            kick = default;
            if (p.IsGoalkeeper) return false;
            int dir = m.AttackDir(p.Team);
            float distGoal = Vec3.FlatDistance(contact, m.OpponentGoal(p.Team));
            float h = contact.Y;
            float ballSpeed = m.Ball.Velocity.Length;

            // Attacking: shoot first time in the box from crosses or sharp passes.
            if (distGoal < 17f && (h > 0.5f || ballSpeed > 9f || m.NearestOpponentDistance(p) < 1.6f))
            {
                float xg = ShotModel.ExpectedGoals(m, p, contact, h > 1.1f);
                if (xg > (h > 1.1f ? 0.04f : 0.07f))
                {
                    kick = BuildShot(p, h > 1.1f ? 0.6f : 0.75f, 0f);
                    kick.Kind = h > 1.1f ? KickKind.Header : (h > 0.45f ? KickKind.Volley : KickKind.Shot);
                    return true;
                }
            }

            // Defending: clear dangerous high balls/crosses in our own third.
            float ownAx = contact.X * dir;
            if (ownAx < -HL + 30f && (h > 1.1f || (m.NearestOpponentDistance(p) < 1.8f && ballSpeed > 8f)))
            {
                Vec3 target = new Vec3((ownAx + 35f) * dir, 0, MathF.Sign(contact.Z == 0 ? 1 : contact.Z) * m.Rng.Range(10f, 25f));
                kick = new PendingKick
                {
                    Kind = h > 1.1f ? KickKind.Header : KickKind.Clearance,
                    Target = Pitch.ClampToField(target, 3f),
                    ReceiverId = -1,
                    Power = 1f,
                    ErrorScale = ErrorScale(p)
                };
                return true;
            }

            // High balls elsewhere: header towards a teammate ahead.
            if (h > 1.6f)
            {
                MatchPlayer mate = null;
                float bestScore = float.MinValue;
                foreach (var r in m.Teams[p.Team].Players)
                {
                    if (!r.IsActive || r == p || r.IsGoalkeeper) continue;
                    float d = Vec3.FlatDistance(r.Pos, p.Pos);
                    if (d < 4f || d > 18f) continue;
                    float score = (r.Pos.X - p.Pos.X) * dir * 0.1f + OpennessAt(r.Pos, p.Team) * 0.3f;
                    if (score > bestScore) { bestScore = score; mate = r; }
                }
                Vec3 t = mate != null ? mate.Pos : p.Pos + new Vec3(dir * 10f, 0, 0);
                kick = new PendingKick { Kind = KickKind.Header, Target = t, ReceiverId = mate?.Id ?? -1, ErrorScale = ErrorScale(p) };
                return true;
            }
            return false;
        }

        private void Jump(MatchPlayer p)
        {
            p.JumpVel = MathUtil.Attr(p.A.Jumping, 2.4f, 3.6f);
            p.JumpHeight = 0.001f;
        }
    }
}
