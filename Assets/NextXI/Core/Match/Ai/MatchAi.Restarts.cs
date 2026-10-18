using System;
using System.Collections.Generic;
using NextXI.Core.Data;

namespace NextXI.Core.Match
{
    /// <summary>Set pieces, restarts, celebrations and the AI coach.</summary>
    internal sealed partial class MatchAi
    {
        public MatchPlayer ChooseTaker(Restart r)
        {
            var team = m.Teams[r.Team];
            MatchPlayer best = null;
            float bestScore = float.MinValue;
            foreach (var p in team.Players)
            {
                if (!p.IsActive) continue;
                float d = Vec3.FlatDistance(p.Pos, r.Spot);
                float score;
                switch (r.Type)
                {
                    case RestartType.GoalKick:
                        score = p.IsGoalkeeper ? 100f : -100f;
                        break;
                    case RestartType.KickOff:
                        score = p.Role == TacticalRole.Striker ? 50f - d : (p.Role == TacticalRole.AttackingMid ? 30f - d : -d);
                        break;
                    case RestartType.Penalty:
                        score = p.IsGoalkeeper ? -100f : p.A.Finishing + p.A.Composure * 0.5f;
                        break;
                    case RestartType.Corner:
                        score = p.IsGoalkeeper ? -100f : p.A.Crossing * 0.6f - d * 0.8f + (p.Role == TacticalRole.CentreBack || p.Role == TacticalRole.Striker ? -40f : 0f);
                        break;
                    case RestartType.FreeKick:
                    {
                        float dg = Vec3.FlatDistance(r.Spot, m.OpponentGoal(r.Team));
                        if (p.IsGoalkeeper) score = r.Spot.X * m.AttackDir(r.Team) < -HL + 20f ? 20f - d : -100f;
                        else if (dg < 32f && !r.Indirect) score = p.A.Curve * 0.5f + p.A.Finishing * 0.3f + p.A.ShotPower * 0.2f - d * 0.1f;
                        else score = -d;
                        break;
                    }
                    case RestartType.ThrowIn:
                        score = p.IsGoalkeeper ? -100f : -d + (p.Role == TacticalRole.FullBack ? 6f : 0f);
                        break;
                    default:
                        score = -d;
                        break;
                }
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return best ?? team.Players[0];
        }

        public Vec3 TakerStandPoint(Restart r)
        {
            Vec3 aim = RestartAim(r);
            if (r.Type == RestartType.ThrowIn) return new Vec3(r.Spot.X, 0, MathF.Sign(r.Spot.Z) * (HW + 0.15f));
            Vec3 back = (r.Spot - aim).FlatNormalized;
            return new Vec3(r.Spot.X, 0, r.Spot.Z) + back * 0.45f;
        }

        private Vec3 RestartAim(Restart r)
        {
            switch (r.Type)
            {
                case RestartType.Penalty:
                case RestartType.Corner:
                    return m.OpponentGoal(r.Team) - new Vec3(m.AttackDir(r.Team) * 8f, 0, 0);
                case RestartType.ThrowIn:
                    return new Vec3(r.Spot.X + m.AttackDir(r.Team) * 4f, 0, 0);
                case RestartType.KickOff:
                    return new Vec3(-m.AttackDir(r.Team) * 10f, 0, 0);
                default:
                    return m.OpponentGoal(r.Team);
            }
        }

        public float RestartFacing(Restart r) => (RestartAim(r) - r.Spot).FlatNormalized.FlatAngle;

        public void SetRestartTargets(Restart r)
        {
            int atk = r.Team;
            int dirA = m.AttackDir(atk);
            Vec3 spot = r.Spot;
            foreach (var p in m.Players)
            {
                if (!p.IsActive) continue;
                p.ClearActionIfInterruptible();
                p.RunTimer = 0;
                p.Jockeying = false;
                p.Shielding = false;
                bool attacking = p.Team == atk;
                Vec3 t;
                if (r.Type == RestartType.KickOff)
                {
                    t = ShapeTarget(p, attacking, Vec3.Zero);
                    int dir = m.AttackDir(p.Team);
                    float ax = MathF.Min(t.X * dir, -1.0f);
                    t = new Vec3(ax * dir, 0, t.Z);
                    if (!attacking && t.FlatLength < Pitch.CentreCircleRadius + 0.5f)
                        t = t.FlatNormalized * (Pitch.CentreCircleRadius + 0.5f);
                }
                else if (p.IsGoalkeeper)
                {
                    int side = -m.AttackDir(p.Team);
                    t = Pitch.GoalCentre(side) - new Vec3(side * (r.Type == RestartType.Penalty && !attacking ? 0.1f : 2.5f), 0, 0);
                    if (r.Type == RestartType.Penalty && !attacking) t = Pitch.GoalCentre(side) - new Vec3(side * 0.2f, 0, 0);
                }
                else t = ShapeTarget(p, attacking, spot);

                if (r.Type == RestartType.Corner) t = CornerTarget(p, attacking, r, t);
                if (r.Type == RestartType.Penalty && !p.IsGoalkeeper)
                {
                    int side = m.AttackDir(atk);
                    float edge = HL - Pitch.PenaltyAreaDepth - 1.5f;
                    float ax = t.X * side;
                    if (ax > edge) t = new Vec3(edge * side, 0, MathUtil.Clamp(t.Z, -18f, 18f));
                }
                if (!attacking && (r.Type == RestartType.FreeKick || r.Type == RestartType.Corner || r.Type == RestartType.KickOff || r.Type == RestartType.ThrowIn))
                {
                    float minD = r.Type == RestartType.ThrowIn ? 2.5f : 9.3f;
                    Vec3 rel = (t - spot).FlatXZ;
                    if (rel.Length < minD) t = new Vec3(spot.X, 0, spot.Z) + (rel.Length > 0.1f ? rel.Normalized : (m.OwnGoal(p.Team) - spot).FlatNormalized) * minD;
                }
                if (r.Type == RestartType.GoalKick && !attacking)
                {
                    int side = -m.AttackDir(atk);
                    if (Pitch.IsInsidePenaltyArea(t, side)) t = new Vec3((HL - Pitch.PenaltyAreaDepth - 1.5f) * side, 0, t.Z);
                }
                p.Target = Pitch.ClampToField(t, 0.8f);
                p.TargetSpeedFactor = 0.62f;
            }
            if (r.Type == RestartType.FreeKick) BuildWall(r);

            var taker = m.FindPlayer(r.TakerId);
            if (taker != null) { taker.Target = TakerStandPoint(r); taker.TargetSpeedFactor = 0.95f; }
            // Kick-off partner stands next to the ball.
            if (r.Type == RestartType.KickOff && taker != null)
            {
                MatchPlayer partner = null;
                float bd = float.MaxValue;
                foreach (var p in m.Teams[atk].Players)
                {
                    if (!p.IsActive || p == taker || p.IsGoalkeeper) continue;
                    float d = Vec3.FlatDistance(p.Target, Vec3.Zero);
                    if (d < bd) { bd = d; partner = p; }
                }
                if (partner != null) partner.Target = new Vec3(-dirA * 1.5f, 0, 2.5f);
            }
        }

        private Vec3 CornerTarget(MatchPlayer p, bool attacking, Restart r, Vec3 fallback)
        {
            int side = m.AttackDir(r.Team);
            float zs = MathF.Sign(r.Spot.Z);
            Vec3 S(float axFromLine, float z) => new Vec3((HL - axFromLine) * side, 0, z);
            if (attacking)
            {
                switch (p.Role)
                {
                    case TacticalRole.Striker: return p.SlotIndex % 2 == 0 ? S(6f, zs * 2f) : S(7f, -zs * 2.5f);
                    case TacticalRole.CentreBack: return p.SlotIndex % 2 == 0 ? S(9f, -zs * 1f) : S(6.5f, -zs * 4.5f);
                    case TacticalRole.AttackingMid:
                    case TacticalRole.Winger: return p.SlotIndex % 2 == 0 ? S(11f, zs * 1f) : S(18f, -zs * 6f);
                    case TacticalRole.CentralMid: return S(18f, zs * 4f);
                    default: return fallback;
                }
            }
            switch (p.Role)
            {
                case TacticalRole.CentreBack: return p.SlotIndex % 2 == 0 ? S(5.5f, zs * 1.5f) : S(6.5f, -zs * 2f);
                case TacticalRole.FullBack: return p.SlotIndex % 2 == 0 ? S(1f, zs * 3.3f) : S(1f, -zs * 3.3f);
                case TacticalRole.DefensiveMid:
                case TacticalRole.CentralMid: return p.SlotIndex % 2 == 0 ? S(9f, zs * 1f) : S(11f, -zs * 3f);
                case TacticalRole.Striker: return S(25f, 0f);
                default: return S(14f, zs * 5f);
            }
        }

        private void BuildWall(Restart r)
        {
            int def = 1 - r.Team;
            Vec3 goal = m.OwnGoal(def);
            float dg = Vec3.FlatDistance(r.Spot, goal);
            if (dg > 32f || r.Indirect) return;
            Vec3 toGoal = (goal - r.Spot).FlatNormalized;
            Vec3 lateral = toGoal.RotateY(MathUtil.Pi * 0.5f);
            int size = dg < 22f ? 4 : 3;
            var candidates = new List<MatchPlayer>();
            foreach (var p in m.Teams[def].Players)
                if (p.IsActive && !p.IsGoalkeeper) candidates.Add(p);
            candidates.Sort((a, b) => Vec3.FlatDistance(a.Target, r.Spot).CompareTo(Vec3.FlatDistance(b.Target, r.Spot)));
            for (int i = 0; i < size && i < candidates.Count; i++)
            {
                float off = (i - (size - 1) * 0.5f) * 0.62f + 0.3f;
                candidates[i].Target = new Vec3(r.Spot.X, 0, r.Spot.Z) + toGoal * 9.3f + lateral * off;
            }
        }

        public void UpdateRestartMovement(float dt)
        {
            Vec3 ballPos = m.Ball.Position;
            foreach (var p in m.Players)
            {
                if (!p.IsActive) continue;
                if (p.Id == m.CurrentRestart.TakerId) { p.Target = TakerStandPoint(m.CurrentRestart); p.TargetSpeedFactor = 0.95f; continue; }
                if (p.UserControlled && !m.UserAutopilot && m.Phase == MatchPhase.SetPiece) continue;
                float d = Vec3.FlatDistance(p.Pos, p.Target);
                p.TargetSpeedFactor = d > 8f ? 0.8f : 0.55f;
            }
        }

        public void OnSetPieceReady(Restart r, MatchPlayer taker)
        {
            _setPieceDelay = r.Type == RestartType.Penalty ? m.Rng.Range(1.4f, 2.2f) : m.Rng.Range(0.7f, 1.5f);
            if (r.Type == RestartType.KickOff) _setPieceDelay = 0.6f;
        }

        public void TakeSetPiece(Restart r, MatchPlayer taker, float elapsed)
        {
            if (elapsed < _setPieceDelay || taker.IsBusy) return;
            int dir = m.AttackDir(taker.Team);
            PendingKick k;
            switch (r.Type)
            {
                case RestartType.KickOff:
                {
                    var mate = NearestMate(taker, 30f, backwardsOk: true);
                    k = new PendingKick { Kind = KickKind.GroundPass, Target = mate?.Pos ?? new Vec3(-dir * 8f, 0, 3f), ReceiverId = mate?.Id ?? -1, Power = 0.3f, ErrorScale = 0.6f };
                    break;
                }
                case RestartType.ThrowIn:
                {
                    var mate = BestOpenMate(taker, 4f, 22f);
                    Vec3 t = mate != null ? mate.Pos + mate.Vel * 0.4f : new Vec3(r.Spot.X + dir * 8f, 0, r.Spot.Z * 0.75f);
                    k = new PendingKick { Kind = KickKind.Throw, Target = t, ReceiverId = mate?.Id ?? -1, Power = 0.5f, ErrorScale = 1f };
                    break;
                }
                case RestartType.GoalKick:
                {
                    var mate = BestOpenMate(taker, 10f, 30f);
                    if (mate != null && OpennessAt(mate.Pos, taker.Team) > 9f && m.Rng.Chance(0.55f))
                        k = new PendingKick { Kind = KickKind.GroundPass, Target = mate.Pos, ReceiverId = mate.Id, Power = 0.5f, ErrorScale = ErrorScale(taker) };
                    else
                    {
                        MatchPlayer fw = null; float far = float.MinValue;
                        foreach (var p in m.Teams[taker.Team].Players)
                            if (p.IsActive && p != taker && p.Pos.X * dir > far && p.Pos.X * dir < HL - 15f) { far = p.Pos.X * dir; fw = p; }
                        k = new PendingKick { Kind = KickKind.Lofted, Target = fw != null ? fw.Pos : new Vec3(dir * 5f, 0, 0), ReceiverId = fw?.Id ?? -1, Power = 1f, ErrorScale = ErrorScale(taker) };
                    }
                    break;
                }
                case RestartType.Corner:
                {
                    float zs = MathF.Sign(r.Spot.Z);
                    float pick = m.Rng.Next();
                    Vec3 t = pick < 0.4f ? new Vec3((HL - 6f) * dir, 0, zs * 2f) : (pick < 0.75f ? new Vec3((HL - 9.5f) * dir, 0, -zs * 1f) : new Vec3((HL - 6.5f) * dir, 0, -zs * 4f));
                    MatchPlayer target = NearestMateTo(taker, t);
                    k = new PendingKick { Kind = KickKind.Cross, Target = t, ReceiverId = target?.Id ?? -1, Power = 0.7f, ErrorScale = ErrorScale(taker), Curl = -zs * dir };
                    break;
                }
                case RestartType.Penalty:
                {
                    k = BuildShot(taker, m.Rng.Range(0.6f, 0.85f), m.Rng.Chance(0.5f) ? 1f : -1f);
                    k.ErrorScale = ErrorScale(taker) * 0.8f;
                    break;
                }
                case RestartType.FreeKick:
                {
                    float dg = Vec3.FlatDistance(r.Spot, m.OpponentGoal(taker.Team));
                    float ax = r.Spot.X * dir;
                    if (!r.Indirect && dg < 28f && MathF.Abs(r.Spot.Z) < 18f)
                    {
                        k = BuildShot(taker, 0.8f, 0f);
                        k.Target = new Vec3(k.Target.X, MathF.Max(k.Target.Y, 1.6f), k.Target.Z);
                        k.Curl = -MathF.Sign(k.Target.Z) * dir;
                    }
                    else if (ax > HL - 35f)
                    {
                        Vec3 t = new Vec3((HL - 9f) * dir, 0, m.Rng.Range(-4f, 4f));
                        var tm = NearestMateTo(taker, t);
                        k = new PendingKick { Kind = KickKind.Cross, Target = t, ReceiverId = tm?.Id ?? -1, Power = 0.7f, ErrorScale = ErrorScale(taker) };
                    }
                    else
                    {
                        var mate = BestOpenMate(taker, 6f, 35f);
                        k = new PendingKick { Kind = mate != null && Vec3.FlatDistance(mate.Pos, taker.Pos) > 28f ? KickKind.Lofted : KickKind.GroundPass, Target = mate?.Pos ?? new Vec3(r.Spot.X + dir * 15f, 0, r.Spot.Z), ReceiverId = mate?.Id ?? -1, Power = 0.5f, ErrorScale = ErrorScale(taker) };
                    }
                    break;
                }
                default:
                {
                    var mate = NearestMate(taker, 25f, true);
                    k = new PendingKick { Kind = KickKind.GroundPass, Target = mate?.Pos ?? taker.Pos + taker.FacingDir * 6f, ReceiverId = mate?.Id ?? -1, Power = 0.4f, ErrorScale = 1f };
                    break;
                }
            }
            taker.Facing = (k.Target - taker.Pos).FlatNormalized.FlatAngle;
            m.StartKick(taker, k);
        }

        private MatchPlayer NearestMate(MatchPlayer p, float maxD, bool backwardsOk)
        {
            MatchPlayer best = null; float bd = maxD;
            foreach (var r in m.Teams[p.Team].Players)
            {
                if (!r.IsActive || r == p || r.IsGoalkeeper) continue;
                float d = Vec3.FlatDistance(r.Pos, p.Pos);
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        private MatchPlayer NearestMateTo(MatchPlayer p, Vec3 point)
        {
            MatchPlayer best = null; float bd = float.MaxValue;
            foreach (var r in m.Teams[p.Team].Players)
            {
                if (!r.IsActive || r == p || r.IsGoalkeeper) continue;
                float d = Vec3.FlatDistance(r.Pos, point);
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        private MatchPlayer BestOpenMate(MatchPlayer p, float minD, float maxD)
        {
            MatchPlayer best = null; float bu = float.MinValue;
            int dir = m.AttackDir(p.Team);
            foreach (var r in m.Teams[p.Team].Players)
            {
                if (!r.IsActive || r == p) continue;
                float d = Vec3.FlatDistance(r.Pos, p.Pos);
                if (d < minD || d > maxD) continue;
                float u = MathF.Min(8f, OpennessAt(r.Pos, p.Team)) * LaneSafety(p.Pos, r.Pos, p.Team) + (r.Pos.X - p.Pos.X) * dir * 0.05f;
                if (r.IsGoalkeeper) u -= 3f;
                if (u > bu) { bu = u; best = r; }
            }
            return best;
        }

        // ================================================================== celebrations

        private MatchPlayer _scorer;
        private int _scoringTeam;

        public void StartCelebration(int team, MatchPlayer scorer)
        {
            _scorer = scorer;
            _scoringTeam = team;
            int dir = m.AttackDir(team);
            foreach (var p in m.Players)
            {
                if (!p.IsActive) continue;
                p.ClearActionIfInterruptible();
                p.RunTimer = 0;
                if (p.Team == team)
                {
                    if (p == scorer)
                    {
                        p.StartAction(PlayerAction.Celebrate, 4.5f, 99f);
                        p.Target = new Vec3((HL - 4f) * dir, 0, MathF.Sign(p.Pos.Z == 0 ? 1 : p.Pos.Z) * (HW - 3f));
                        p.TargetSpeedFactor = 0.85f;
                    }
                    else p.TargetSpeedFactor = 0.7f;
                }
                else
                {
                    if (!p.IsGoalkeeper || true) p.StartAction(PlayerAction.Dejected, 3.5f, 99f);
                    p.Target = p.Pos;
                    p.TargetSpeedFactor = 0.2f;
                }
            }
        }

        public void UpdateCelebration(float dt)
        {
            foreach (var p in m.Players)
            {
                if (!p.IsActive || p.Team != _scoringTeam || p == _scorer) continue;
                if (_scorer != null && !p.IsGoalkeeper)
                {
                    Vec3 to = (_scorer.Pos - p.Pos).FlatXZ;
                    p.Target = to.Length > 2f ? _scorer.Pos - to.Normalized * 1.2f : p.Pos;
                }
            }
        }

        // ================================================================== coach

        /// <summary>AI coach substitutions, evaluated at stoppages.</summary>
        public void CoachThink()
        {
            float minute = m.Clock.TotalVirtualSeconds / 60f;
            // The user's athlete starting on the bench comes on at the coach's chosen minute.
            var u = m.UserPlayer;
            if (u != null && !u.OnPitch && !u.SentOff && m.UserSubInMinute >= 0f && minute >= m.UserSubInMinute)
            {
                var team = m.Teams[u.Team];
                int bench = team.Bench.IndexOf(u);
                if (bench >= 0 && team.SubstitutionsUsed < m.Settings.MaxSubstitutions)
                {
                    var group = PositionInfo.Group(u.Profile.Position);
                    MatchPlayer off = null; float worst = float.MaxValue;
                    foreach (var p in team.Players)
                    {
                        if (!p.IsActive || p.IsGoalkeeper != (group == PositionGroup.Goalkeeper)) continue;
                        float score = (PositionInfo.Group(p.Position) == group ? 0f : 20f) + p.Stamina * 10f + p.Stats.Rating;
                        if (score < worst) { worst = score; off = p; }
                    }
                    if (off != null && m.RequestSubstitution(u.Team, off.Id, bench)) m.UserSubInMinute = -1f;
                }
            }
            for (int t = 0; t < 2; t++)
            {
                var team = m.Teams[t];
                if (!team.Sheet.AiControlledCoach) continue;
                if (team.SubstitutionsUsed >= m.Settings.MaxSubstitutions || team.Bench.Count == 0) continue;
                foreach (var p in team.Players)
                {
                    if (!p.IsActive) continue;
                    bool isUser = p == m.UserPlayer;
                    if (isUser && !m.AllowCoachToSubUser) continue;
                    bool injured = p.Injured;
                    bool tired = minute > 58f && p.Stamina < (isUser ? 0.36f : 0.5f);
                    if (isUser && minute > 70f && p.Stats.Rating < 5.2f && p.Stamina < 0.6f) tired = true;
                    if (!injured && !tired) continue;
                    int bench = BestBenchFor(team, p);
                    if (bench < 0) continue;
                    if (m.RequestSubstitution(t, p.Id, bench)) break; // one change per stoppage
                }
            }
        }

        private int BestBenchFor(MatchTeam team, MatchPlayer off)
        {
            int best = -1; float bs = float.MinValue;
            var wantGroup = PositionInfo.Group(off.Position);
            for (int i = 0; i < team.Bench.Count; i++)
            {
                var b = team.Bench[i];
                var g = PositionInfo.Group(b.Profile.Position);
                if (off.IsGoalkeeper != (g == PositionGroup.Goalkeeper)) continue;
                float s = b.Profile.Attributes.Overall(off.Position) + (g == wantGroup ? 10f : 0f) + (b.Profile.CanPlay(off.Position) ? 8f : 0f);
                if (s > bs) { bs = s; best = i; }
            }
            return best;
        }
    }
}
