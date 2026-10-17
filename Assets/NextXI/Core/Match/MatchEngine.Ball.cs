using System;
using System.Collections.Generic;
using NextXI.Core.Data;

namespace NextXI.Core.Match
{
    /// <summary>Ball control, kicks, tackles, saves, offside and movement integration.</summary>
    public sealed partial class MatchEngine
    {
        private bool _pendingPass;
        private MatchPlayer _passFrom;
        private int _passReceiverId = -1;
        private MatchPlayer _lastCompletedPassFrom, _lastCompletedPassTo;
        private float _lastCompletedPassTime = -100f;
        private bool _activeShot;
        private float _activeShotXg;
        private MatchPlayer _activeShooter;
        private readonly HashSet<int> _offsideIds = new HashSet<int>();
        private int _offsideTeam = -1;
        private readonly List<MatchPlayer> _contactCandidates = new List<MatchPlayer>();

        /// <summary>Receiver of the pass currently travelling (-1 when none).</summary>
        public int PassReceiverId => _pendingPass ? _passReceiverId : -1;
        public bool PassInFlight => _pendingPass;
        public bool ShotInFlight => _activeShot;

        // ------------------------------------------------------------------ kicks

        public static bool IsKickAction(PlayerAction a) =>
            a == PlayerAction.Pass || a == PlayerAction.LoftedPass || a == PlayerAction.Cross || a == PlayerAction.Shot ||
            a == PlayerAction.Volley || a == PlayerAction.Header || a == PlayerAction.Clearance || a == PlayerAction.Throw ||
            a == PlayerAction.KeeperThrow || a == PlayerAction.KeeperKick;

        /// <summary>Begin a kick by the ball owner/holder. The ball leaves the foot at the animation contact frame.</summary>
        public bool StartKick(MatchPlayer p, PendingKick k)
        {
            if (p == null || !p.IsActive || p.Stun > 0f || p.IsBusy) return false;
            bool firstTime = k.Kind == KickKind.Header || k.Kind == KickKind.Volley;
            if (!firstTime && Owner != p && Holder != p) return false;

            PlayerAction action; float dur, contact;
            switch (k.Kind)
            {
                case KickKind.GroundPass: action = PlayerAction.Pass; dur = 0.42f; contact = 0.15f; break;
                case KickKind.ThroughPass: action = PlayerAction.Pass; dur = 0.45f; contact = 0.17f; break;
                case KickKind.Lofted: action = PlayerAction.LoftedPass; dur = 0.6f; contact = 0.24f; break;
                case KickKind.Cross: action = PlayerAction.Cross; dur = 0.62f; contact = 0.25f; break;
                case KickKind.Shot: action = PlayerAction.Shot; dur = 0.62f; contact = 0.2f + 0.1f * MathUtil.Clamp01(k.Power); break;
                case KickKind.Clearance: action = PlayerAction.Clearance; dur = 0.55f; contact = 0.2f; break;
                case KickKind.Throw: action = PlayerAction.Throw; dur = 0.9f; contact = 0.42f; break;
                case KickKind.KeeperThrow: action = PlayerAction.KeeperThrow; dur = 0.8f; contact = 0.34f; break;
                case KickKind.KeeperKick: action = PlayerAction.KeeperKick; dur = 0.95f; contact = 0.42f; break;
                case KickKind.Header: action = PlayerAction.Header; dur = 0.55f; contact = 0f; break;
                default: action = PlayerAction.Volley; dur = 0.5f; contact = 0f; break;
            }
            p.Kick = k;
            p.StartAction(action, dur, contact);
            Vec3 toTarget = (k.Target - p.Pos).FlatNormalized;
            float rel = Vec3.SignedFlatAngle(p.FacingDir, toTarget);
            // Right-footed players use the left foot for balls going sharply to their left (and vice-versa),
            // more willingly the better their weak foot.
            bool right = p.Profile.PreferredFoot == Foot.Right;
            float threshold = 0.45f + (5 - p.A.WeakFoot) * 0.15f;
            p.KickWithWeakFoot = right ? rel > threshold : rel < -threshold;
            if (firstTime)
            {
                p.ContactDone = true;
                ExecuteKick(p);
            }
            return true;
        }

        private void ExecuteKick(MatchPlayer p)
        {
            var k = p.Kick;
            bool fromHands = k.Kind == KickKind.Throw || k.Kind == KickKind.KeeperThrow || k.Kind == KickKind.KeeperKick;
            if (fromHands)
            {
                if (Holder != p) return;
                Holder = null;
                Ball.Held = false;
                Ball.Position = p.Pos + p.FacingDir * 0.4f + new Vec3(0, k.Kind == KickKind.KeeperKick ? 0.6f : (k.Kind == KickKind.Throw ? 2.05f : 1.7f), 0);
            }
            else
            {
                Vec3 foot = p.FootPoint(p.KickWithWeakFoot);
                float dist = Vec3.FlatDistance(Ball.Position, foot);
                bool headerReach = k.Kind == KickKind.Header && Ball.Position.Y > 1.0f;
                if (!headerReach && (dist > 1.35f || Ball.Position.Y > 1.25f))
                {
                    // Whiff: the ball wasn't where the foot arrived.
                    if (Owner == p && dist > 1.6f) Owner = null;
                    return;
                }
            }

            Vec3 from = Ball.Position;
            Vec3 target = k.Target;
            var receiver = k.ReceiverId >= 0 ? FindPlayer(k.ReceiverId) : null;
            Vec3 v;
            float power = MathUtil.Clamp01(k.Power);
            float baseErr;
            switch (k.Kind)
            {
                case KickKind.GroundPass:
                {
                    float arrival = MathUtil.Lerp(5f, 9.5f, power);
                    if (receiver != null && receiver.Team == p.Team)
                        target = KickSolver.LeadTarget(from, receiver.Pos, receiver.Vel * 0.8f, KickSolver.GroundPassSpeed(Vec3.FlatDistance(from, receiver.Pos), arrival));
                    target = Pitch.ClampToField(target, 0.8f);
                    v = KickSolver.GroundPass(from, target, arrival);
                    baseErr = MathUtil.Attr(p.A.ShortPassing, 0.085f, 0.014f);
                    if (Vec3.FlatDistance(from, target) > 28f) baseErr = MathUtil.Attr(p.A.LongPassing, 0.09f, 0.02f);
                    break;
                }
                case KickKind.ThroughPass:
                {
                    target = Pitch.ClampToField(target, 1.5f);
                    float arrival = MathUtil.Lerp(2.5f, 5f, power);
                    v = KickSolver.GroundPass(from, target, arrival);
                    baseErr = MathUtil.Attr(p.A.Vision, 0.09f, 0.018f);
                    if (p.Profile.HasTrait(Traits.ThroughBallVision)) baseErr *= 0.7f;
                    break;
                }
                case KickKind.Lofted:
                case KickKind.Clearance:
                case KickKind.KeeperKick:
                {
                    target = Pitch.ClampToField(target, 1f);
                    float d = Vec3.FlatDistance(from, target);
                    float T = k.Kind == KickKind.Lofted ? MathUtil.Clamp(0.75f + d / 26f, 1.0f, 2.7f) : MathUtil.Clamp(1.2f + d / 30f, 1.8f, 3.3f);
                    v = KickSolver.Lofted(from, target, T, Pitch);
                    baseErr = k.Kind == KickKind.KeeperKick ? MathUtil.Attr(p.A.GkKicking, 0.11f, 0.03f) : MathUtil.Attr(p.A.LongPassing, 0.11f, 0.024f);
                    if (k.Kind == KickKind.Clearance) baseErr *= 1.6f;
                    break;
                }
                case KickKind.Cross:
                {
                    target = Pitch.ClampToField(target, 1f);
                    float d = Vec3.FlatDistance(from, target);
                    float T = MathUtil.Clamp(0.8f + d / 32f, 1.0f, 1.9f);
                    v = KickSolver.Lofted(from, target, T, Pitch, 1.55f);
                    baseErr = MathUtil.Attr(p.A.Crossing, 0.12f, 0.025f);
                    if (p.Profile.HasTrait(Traits.Crosser)) baseErr *= 0.75f;
                    break;
                }
                case KickKind.Throw:
                case KickKind.KeeperThrow:
                {
                    target = Pitch.ClampToField(target, 0.8f);
                    float d = Vec3.FlatDistance(from, target);
                    float T = MathUtil.Clamp(0.45f + d / 22f, 0.55f, 1.4f);
                    v = KickSolver.Lofted(from, target, T, Pitch, 0.5f);
                    baseErr = 0.035f;
                    break;
                }
                case KickKind.Header:
                {
                    float speed = MathUtil.Attr(p.A.Heading, 9f, 17f);
                    if (k.IsShot) v = KickSolver.Driven(from, target, speed, MathF.Max(0.2f, target.Y), Pitch);
                    else v = (target - from).FlatNormalized * speed * 0.8f + new Vec3(0, 3.5f, 0);
                    baseErr = MathUtil.Attr(p.A.Heading, 0.16f, 0.045f);
                    break;
                }
                case KickKind.Volley:
                {
                    float speed = MathUtil.Attr(p.A.ShotPower, 17f, 27f);
                    v = KickSolver.Driven(from, target, speed, MathF.Max(0.2f, target.Y), Pitch);
                    baseErr = MathUtil.Attr(p.A.Finishing, 0.17f, 0.05f);
                    break;
                }
                default: // Shot
                {
                    float dist = Vec3.FlatDistance(from, target);
                    float speed = MathUtil.Attr(p.A.ShotPower, 19f, 30.5f) * MathUtil.Lerp(0.68f, 1.05f, power);
                    if (p.Profile.HasTrait(Traits.PowerShot) && dist > 18f) speed *= 1.06f;
                    v = KickSolver.Driven(from, target, speed, MathF.Max(0.15f, target.Y), Pitch);
                    baseErr = dist > 20f ? MathUtil.Attr(p.A.LongShots, 0.12f, 0.026f) : MathUtil.Attr(p.A.Finishing, 0.11f, 0.02f);
                    baseErr *= 1f + 1.3f * MathF.Max(0f, power - 0.78f);
                    if (p.Profile.HasTrait(Traits.FinesseShot) && power < 0.75f) baseErr *= 0.78f;
                    break;
                }
            }

            // Execution error: attributes, pressure, body shape, weak foot, fatigue, speed.
            float pressureDist = NearestOpponentDistance(p);
            float pressure = 1f + 0.65f * MathUtil.Clamp01((2.6f - pressureDist) / 2.6f) * (1f - p.A.Composure / 160f);
            Vec3 kickDir = v.FlatNormalized;
            float bodyAngle = MathF.Abs(Vec3.SignedFlatAngle(p.FacingDir, kickDir));
            float body = fromHands ? 1f : 1f + 0.9f * MathUtil.Clamp01((bodyAngle - 0.5f) / 1.6f);
            float weak = p.KickWithWeakFoot ? 1f + (5 - p.A.WeakFoot) * 0.2f : 1f;
            float fatigue = 1f + (1f - p.Stamina) * 0.45f;
            float moving = 1f + MathUtil.Clamp01(p.Speed / 9f) * 0.25f;
            bool firstTime = Owner != p && Holder == null && !fromHands && k.Kind != KickKind.Header;
            float ft = firstTime ? (p.Profile.HasTrait(Traits.FirstTimePass) ? 1.1f : 1.35f) : 1f;
            float err = baseErr * pressure * body * weak * fatigue * moving * ft * MathF.Max(0.2f, k.ErrorScale <= 0 ? 1f : k.ErrorScale);

            float yaw = Rng.Gaussian() * err;
            v = v.RotateY(yaw);
            float sp = v.Length;
            v = v * (1f + Rng.Gaussian() * err * 0.45f);
            v.Y += Rng.Gaussian() * err * sp * 0.45f;
            if (k.Kind == KickKind.GroundPass || k.Kind == KickKind.ThroughPass) v.Y = MathF.Max(0f, v.Y * 0.3f);

            Ball.Velocity = v.ClampLength(BallPhysics.MaxSpeed);
            Ball.Spin = new Vec3(0, k.Curl * 6f, 0);
            if (Owner == p) Owner = null;
            p.TouchCooldown = 0.32f;
            TrajectoryVersion++;

            bool restartExempt = Phase == MatchPhase.SetPiece &&
                                 (CurrentRestart.Type == RestartType.ThrowIn || CurrentRestart.Type == RestartType.Corner || CurrentRestart.Type == RestartType.GoalKick);
            bool offsidePlay = OnTouch(p, deliberate: true);
            if (offsidePlay) return;
            LastKicker = p;
            LastKickKind = k.Kind;
            if (!restartExempt) SnapshotOffside(p);

            var team = Teams[p.Team];
            if (k.IsShot || k.Kind == KickKind.Shot || k.Kind == KickKind.Volley)
            {
                _pendingPass = false;
                int side = AttackDir(p.Team);
                float xg = ShotModel.ExpectedGoals(this, p, from, k.Kind == KickKind.Header);
                p.Stats.Shots++; team.Stats.Shots++;
                p.Stats.XG += xg; team.Stats.XG += xg;
                if (_lastCompletedPassTo == p && SimTime - _lastCompletedPassTime < 6f && _lastCompletedPassFrom != null && _lastCompletedPassFrom.Team == p.Team)
                    _lastCompletedPassFrom.Stats.KeyPasses++;
                bool onTarget = PredictGoalLineCrossing(Ball, side, out _, out _, true);
                Emit(MatchEventType.Shot, p.Team, p, from, xg);
                if (onTarget)
                {
                    p.Stats.ShotsOnTarget++; team.Stats.ShotsOnTarget++;
                    Emit(MatchEventType.ShotOnTarget, p.Team, p, from, xg);
                }
                _activeShot = true;
                _activeShotXg = xg;
                _activeShooter = p;
                _activeShotOnTarget = onTarget;
                if (k.Kind == KickKind.Header) Emit(MatchEventType.Header, p.Team, p, from, 1);
            }
            else if (k.Kind == KickKind.Clearance || (k.Kind == KickKind.Header && !k.IsShot && receiver == null))
            {
                _pendingPass = false;
                p.Stats.Clearances++;
                if (k.Kind == KickKind.Header) Emit(MatchEventType.Header, p.Team, p, from, 0);
            }
            else
            {
                _pendingPass = true;
                _passFrom = p;
                _passReceiverId = receiver != null ? receiver.Id : -1;
                if (k.Kind != KickKind.Throw)
                {
                    p.Stats.Passes++; team.Stats.Passes++;
                }
                if (k.Kind == KickKind.Cross) { p.Stats.Crosses++; Emit(MatchEventType.Cross, p.Team, p, from, v.Length, receiver); }
                else Emit(MatchEventType.Pass, p.Team, p, from, v.Length, receiver);
            }
        }

        private bool _activeShotOnTarget;

        /// <summary>Predicts whether/where the ball crosses the goal line of `side`.</summary>
        public bool PredictGoalLineCrossing(BallState b, int side, out Vec3 point, out float time, bool requireMouth)
        {
            const float dt = 1f / 60f;
            float line = side * Pitch.HalfLength;
            point = b.Position; time = 0;
            for (int i = 0; i < 240; i++)
            {
                Vec3 prev = b.Position;
                BallPhysics.Step(ref b, dt, Pitch);
                time += dt;
                if ((prev.X - line) * side < 0 && (b.Position.X - line) * side >= 0)
                {
                    float t = MathUtil.InverseLerp(prev.X * side, b.Position.X * side, line * side);
                    point = Vec3.Lerp(prev, b.Position, t);
                    if (!requireMouth) return true;
                    return MathF.Abs(point.Z) < Pitch.HalfGoalWidth && point.Y < Pitch.GoalHeight;
                }
                if (b.Velocity.FlatLength < 0.5f) return false;
            }
            return false;
        }

        // ------------------------------------------------------------------ tackles

        public bool StartTackle(MatchPlayer p)
        {
            if (p == null || !p.CanAct || p.IsGoalkeeper && Holder == p) return false;
            p.StartAction(PlayerAction.Tackle, 0.5f, 0.2f);
            return true;
        }

        public bool StartSlide(MatchPlayer p, Vec3 dir)
        {
            if (p == null || !p.CanAct) return false;
            p.ActionDir = dir.FlatNormalized;
            if (p.ActionDir.SqrLength < 0.5f) p.ActionDir = p.FacingDir;
            p.Facing = p.ActionDir.FlatAngle;
            p.StartAction(PlayerAction.Slide, 1.05f, 0.12f);
            p.ActionSpeed = MathF.Max(p.Speed, 6.2f);
            return true;
        }

        private void ResolveTackle(MatchPlayer def, bool sliding)
        {
            var owner = Owner;
            Vec3 foot = def.FootPoint() + (sliding ? def.ActionDir * 0.5f : Vec3.Zero);
            float dBall = Vec3.FlatDistance(Ball.Position, foot);
            float reach = sliding ? 1.25f : 1.0f;
            def.Stats.Tackles++;
            Teams[def.Team].Stats.Tackles++;

            if (Ball.Position.Y > 0.8f || dBall > reach)
            {
                if (!sliding) def.Stun = 0.35f;
                if (owner != null && owner.Team != def.Team && Vec3.FlatDistance(def.Pos, owner.Pos) < 0.95f && Rng.Chance(sliding ? 0.6f : 0.25f))
                    TackleFoul(def, owner, sliding);
                return;
            }

            if (owner == null || owner.Team == def.Team)
            {
                // Loose ball: the tackle simply plays it.
                Ball.Velocity = (sliding ? def.ActionDir : def.FacingDir) * Rng.Range(4f, 8f);
                OnTouch(def, true);
                return;
            }

            Vec3 toDef = (def.Pos - owner.Pos).FlatNormalized;
            float behind = Vec3.Dot(owner.FacingDir, toDef); // -1 = from behind
            bool fromBehind = behind < -0.5f;
            int tackleAttr = sliding ? def.A.SlidingTackle : def.A.StandingTackle;
            float p = 0.52f + (tackleAttr - owner.A.Dribbling) / 140f + (def.A.Strength - owner.A.Strength) / 450f;
            if (owner.Shielding) p -= owner.Profile.HasTrait(Traits.Shielder) ? 0.25f : 0.15f;
            if (Vec3.FlatDistance(Ball.Position, owner.Pos) > 0.9f) p += 0.15f;
            if (def.Profile.HasTrait(Traits.CleanTackler)) p += 0.06f;
            if (fromBehind) p -= 0.12f;
            p = MathUtil.Clamp(p, 0.08f, 0.9f);

            float foulP = 0.05f + def.A.Aggression / 1100f - tackleAttr / 900f + (fromBehind ? 0.32f : 0f) + (sliding ? 0.06f : 0f);
            if (def.Profile.HasTrait(Traits.CleanTackler)) foulP *= 0.5f;
            foulP = MathUtil.Clamp(foulP, 0.01f, 0.7f);

            if (Rng.Chance(p))
            {
                def.Stats.TacklesWon++;
                Teams[def.Team].Stats.TacklesWon++;
                Emit(MatchEventType.TackleWon, def.Team, def, Ball.Position, 0, owner);
                Owner = null;
                owner.Stun = 0.3f;
                if (!sliding && Rng.Chance(MathUtil.Attr(def.A.BallControl, 0.35f, 0.75f)))
                {
                    Ball.Velocity = def.Vel * 0.5f;
                    Ball.Position = new Vec3(foot.X, BallPhysics.Radius, foot.Z);
                    OnTouch(def, true);
                    if (Phase == MatchPhase.Playing) { Owner = def; def.NextTouchTime = SimTime + 0.2f; }
                }
                else
                {
                    Vec3 dir = (sliding ? def.ActionDir : (Ball.Position - owner.Pos).FlatNormalized).RotateY(Rng.Range(-0.6f, 0.6f));
                    Ball.Velocity = dir * Rng.Range(4f, sliding ? 9f : 6.5f);
                    OnTouch(def, true);
                    def.TouchCooldown = 0.25f;
                }
            }
            else if (Vec3.FlatDistance(def.Pos, owner.Pos) < 1.1f && Rng.Chance(foulP))
            {
                TackleFoul(def, owner, sliding && fromBehind);
            }
            else
            {
                Emit(MatchEventType.Tackle, def.Team, def, Ball.Position, 0, owner);
                owner.Stats.DribblesWon++;
                if (!sliding) def.Stun = 0.45f;
            }
        }

        private void TackleFoul(MatchPlayer def, MatchPlayer victim, bool reckless)
        {
            if (!Settings.FoulsEnabled) { def.Stun = 0.4f; return; }
            victim.StartAction(PlayerAction.Fall, 1.4f, 99f);
            victim.Stun = 1.4f;
            Vec3 where = victim.Pos;
            CallFoul(def, victim, where, reckless);
        }

        // ------------------------------------------------------------------ action update

        private void UpdateAction(MatchPlayer p, float dt)
        {
            if (p.TouchCooldown > 0) p.TouchCooldown -= dt;
            if (p.Stun > 0) p.Stun -= dt;
            if (p.QueuedFirstTimeTimer > 0) p.QueuedFirstTimeTimer -= dt;
            if (p.CallForPassTimer > 0) p.CallForPassTimer -= dt;
            if (p.CallForThroughTimer > 0) p.CallForThroughTimer -= dt;

            if (p.JumpHeight > 0f || p.JumpVel > 0f)
            {
                p.JumpVel -= 9.81f * dt;
                p.JumpHeight += p.JumpVel * dt;
                if (p.JumpHeight <= 0f) { p.JumpHeight = 0f; p.JumpVel = 0f; }
            }

            if (p.Action == PlayerAction.None) return;
            p.ActionTime += dt;

            if (IsKickAction(p.Action))
            {
                if (!p.ContactDone && p.ActionTime >= p.ContactTime)
                {
                    p.ContactDone = true;
                    ExecuteKick(p);
                }
            }
            else if (p.Action == PlayerAction.Tackle)
            {
                if (!p.ContactDone && p.ActionTime >= p.ContactTime && Phase == MatchPhase.Playing)
                {
                    p.ContactDone = true;
                    ResolveTackle(p, false);
                }
            }
            else if (p.Action == PlayerAction.Slide)
            {
                if (!p.ContactDone && p.ActionTime >= p.ContactTime && p.ActionTime < 0.62f && Phase == MatchPhase.Playing)
                {
                    Vec3 foot = p.Pos + p.ActionDir * 0.9f;
                    bool ballNear = Vec3.FlatDistance(Ball.Position, foot) < 1.0f && Ball.Position.Y < 0.6f && Holder == null;
                    bool bodyNear = Owner != null && Owner.Team != p.Team && Vec3.FlatDistance(Owner.Pos, foot) < 0.8f;
                    if (ballNear || bodyNear)
                    {
                        p.ContactDone = true;
                        ResolveTackle(p, true);
                    }
                }
            }

            if (p.ActionTime >= p.ActionDuration)
            {
                if (p.Action == PlayerAction.Slide) p.Stun = MathF.Max(p.Stun, 0.45f);
                if (p.Action == PlayerAction.KeeperDive) p.Stun = MathF.Max(p.Stun, 0.5f);
                p.ClearAction();
            }
        }

        // ------------------------------------------------------------------ movement

        private void MovePlayer(MatchPlayer p, float dt)
        {
            Vec3 vel = p.Vel;
            switch (p.Action)
            {
                case PlayerAction.Slide:
                {
                    float s = MathUtil.Lerp(p.ActionSpeed, 0f, MathF.Pow(p.ActionProgress, 0.6f));
                    p.Vel = p.ActionDir * s;
                    p.Pos = p.Pos + p.Vel * dt;
                    ClampToWorld(p);
                    return;
                }
                case PlayerAction.KeeperDive:
                {
                    // Load (push-off) phase, then explosive lateral push to the dive point, then the body lands.
                    if (p.ActionTime < 0.12f) { p.Vel = Vec3.Zero; return; }
                    Vec3 toDive = (p.Target - p.Pos).FlatXZ;
                    float remaining = toDive.Length;
                    float s = remaining > 0.05f ? MathF.Min(p.ActionSpeed, remaining / dt) : 0f;
                    p.Vel = remaining > 0.05f ? toDive / remaining * s : Vec3.Zero;
                    p.Pos = p.Pos + p.Vel * dt;
                    ClampToWorld(p);
                    return;
                }
                case PlayerAction.Fall:
                    p.Vel = MoveTowardsVec(vel, Vec3.Zero, 9f * dt);
                    p.Pos = p.Pos + p.Vel * dt;
                    return;
            }

            if (p.Stun > 0f)
            {
                p.Vel = MoveTowardsVec(vel, Vec3.Zero, 7f * dt);
                p.Pos = p.Pos + p.Vel * dt;
                ClampToWorld(p);
                return;
            }

            Vec3 to = (p.Target - p.Pos).FlatXZ;
            float dist = to.Length;
            float cap = p.MaxSpeed * MathUtil.Clamp01(p.TargetSpeedFactor);
            if (Owner == p) cap *= MathUtil.Attr(p.A.Dribbling, 0.8f, 0.94f);
            if (p.Shielding) cap = MathF.Min(cap, 2.3f);
            if (p.Jockeying) cap = MathF.Min(cap, 4.6f);
            if (IsKickAction(p.Action) && !p.ContactDone) cap *= 0.45f;
            else if (IsKickAction(p.Action)) cap *= 0.7f;
            if (p.Action == PlayerAction.Tackle) cap *= 0.7f;
            if (p.Action == PlayerAction.Throw || p.Action == PlayerAction.KeeperThrow || p.Action == PlayerAction.KeeperKick) cap = 0.8f;
            if (p.Action == PlayerAction.KeeperCatch) cap = 1.0f;
            if (Holder == p && Phase == MatchPhase.SetPiece) cap = 0f;

            float brake = p.Acceleration * 1.7f;
            float desiredSpeed = dist < 0.15f ? 0f : MathF.Min(cap, MathF.Sqrt(2f * brake * 0.7f * MathF.Max(0f, dist - 0.1f)));
            Vec3 desired = dist > 1e-4f ? to * (desiredSpeed / dist) : Vec3.Zero;

            Vec3 dv = desired - vel;
            float speed = vel.FlatLength;
            Vec3 newVel;
            if (speed > 0.3f)
            {
                Vec3 vd = vel / speed;
                float alongMag = Vec3.Dot(dv, vd);
                Vec3 along = vd * alongMag;
                Vec3 perp = dv - along;
                float maxAlong = (alongMag >= 0 ? p.Acceleration : brake) * dt;
                float lateral = MathUtil.Attr(p.A.Agility, 7f, 12.5f) * MathUtil.Attr(p.A.Balance, 0.85f, 1.05f);
                if (Owner == p) lateral *= MathUtil.Attr(p.A.Dribbling, 0.8f, 1.0f);
                float maxPerp = lateral * dt;
                newVel = vel + along.ClampLength(maxAlong) + perp.ClampLength(maxPerp);
            }
            else newVel = vel + dv.ClampLength(p.Acceleration * dt);

            p.Vel = new Vec3(newVel.X, 0, newVel.Z);
            p.Pos = p.Pos + p.Vel * dt;
            ClampToWorld(p);
            UpdateFacing(p, dt);
        }

        private void UpdateFacing(MatchPlayer p, float dt)
        {
            float speed = p.Speed;
            Vec3 look;
            float rate = MathUtil.Attr(p.A.Agility, 7f, 12f);
            if (IsKickAction(p.Action) && !p.ContactDone && p.Action != PlayerAction.Header && p.Action != PlayerAction.Volley)
            {
                look = (p.Kick.Target - p.Pos).FlatNormalized;
                rate = 13f;
            }
            else if (p.Shielding)
            {
                var opp = NearestOpponent(p);
                look = opp != null ? (p.Pos - opp.Pos).FlatNormalized : p.FacingDir;
            }
            else if (p.Jockeying || (speed < 3.2f && Owner != p))
            {
                look = (Ball.Position - p.Pos).FlatNormalized;
                if (speed > 1.0f && Vec3.Dot(look, p.Vel / speed) < -0.3f && !p.Jockeying) look = p.Vel / speed;
            }
            else if (speed > 0.4f) look = p.Vel / speed;
            else look = p.FacingDir;

            if (speed > 6f) rate *= 0.6f;
            if (look.SqrLength < 0.5f) return;
            p.Facing = MathUtil.MoveTowardsAngle(p.Facing, look.FlatAngle, rate * dt);
        }

        private void ClampToWorld(MatchPlayer p)
        {
            float mx = Pitch.HalfLength + 4f, mz = Pitch.HalfWidth + 4f;
            if (!p.Pos.IsFinite) { p.Pos = Vec3.Zero; p.Vel = Vec3.Zero; Log.Error("Match", "Non-finite player position reset: " + p); }
            p.Pos = new Vec3(MathUtil.Clamp(p.Pos.X, -mx, mx), 0, MathUtil.Clamp(p.Pos.Z, -mz, mz));
        }

        private static Vec3 MoveTowardsVec(Vec3 a, Vec3 b, float maxDelta)
        {
            Vec3 d = b - a;
            float l = d.Length;
            return l <= maxDelta || l < 1e-6f ? b : a + d / l * maxDelta;
        }

        private void SeparatePlayers()
        {
            const float minDist = 0.62f;
            int n = Players.Count;
            for (int i = 0; i < n; i++)
            {
                var a = Players[i];
                if (!a.IsActive) continue;
                for (int j = i + 1; j < n; j++)
                {
                    var b = Players[j];
                    if (!b.IsActive) continue;
                    Vec3 d = (b.Pos - a.Pos).FlatXZ;
                    float l2 = d.SqrLength;
                    if (l2 >= minDist * minDist) continue;
                    // Sliding/diving bodies and fallen players do not push (they are on the ground).
                    bool aGround = a.Action == PlayerAction.Slide || a.Action == PlayerAction.KeeperDive || a.Action == PlayerAction.Fall;
                    bool bGround = b.Action == PlayerAction.Slide || b.Action == PlayerAction.KeeperDive || b.Action == PlayerAction.Fall;
                    if (aGround || bGround) continue;
                    float l = MathF.Sqrt(l2);
                    Vec3 nrm = l > 1e-4f ? d / l : Vec3.FromAngle(i * 1.3f);
                    float overlap = minDist - l;
                    float sa = a.A.Strength + 30f, sb = b.A.Strength + 30f;
                    float wa = sb / (sa + sb), wb = 1f - wa;
                    a.Pos = a.Pos - nrm * (overlap * wa);
                    b.Pos = b.Pos + nrm * (overlap * wb);
                    // Remove approaching velocity so bodies don't keep driving into each other.
                    float va = Vec3.Dot(a.Vel, nrm), vb = Vec3.Dot(b.Vel, nrm);
                    if (va - vb > 0)
                    {
                        float avg = (va * sa + vb * sb) / (sa + sb);
                        a.Vel = a.Vel - nrm * (va - avg);
                        b.Vel = b.Vel - nrm * (vb - avg);
                    }
                }
            }
        }

        // ------------------------------------------------------------------ ball control

        private void SyncHeldBall()
        {
            var h = Holder;
            float height = h.Action == PlayerAction.Throw ? 2.05f : (h.IsGoalkeeper ? 1.05f : 2.05f);
            Ball.Position = h.Pos + h.FacingDir * 0.28f + new Vec3(0, height, 0);
            Ball.Velocity = Vec3.Zero;
            Ball.Spin = Vec3.Zero;
            Ball.Held = true;
        }

        private void UpdateBallControl(float dt)
        {
            if (Holder != null)
            {
                if (!Holder.IsActive) { Holder = null; Ball.Held = false; }
                else { SyncHeldBall(); return; }
            }
            var o = Owner;
            if (o == null) return;
            if (!o.IsActive || o.Stun > 0f || o.Action == PlayerAction.Fall) { Owner = null; return; }

            float d = Vec3.FlatDistance(Ball.Position, o.Pos);
            if (d > 2.4f || Ball.Position.Y > 1.4f) { Owner = null; return; }

            Vec3 foot = o.FootPoint(o.KickWithWeakFoot && IsKickAction(o.Action));
            if (IsKickAction(o.Action) && !o.ContactDone)
            {
                // Wind-up: bring the ball to the striking foot so the animation contact lines up.
                Vec3 p = Vec3.Lerp(Ball.Position, new Vec3(foot.X, BallPhysics.Radius, foot.Z), MathUtil.Clamp01(14f * dt));
                Ball.Position = p;
                Ball.Velocity = o.Vel;
                return;
            }

            float speed = o.Speed;
            Vec3 dir = o.DribbleDir.SqrLength > 0.5f ? o.DribbleDir : (speed > 0.5f ? o.Vel / speed : o.FacingDir);
            float desiredSpeed = MathF.Min(o.TargetSpeedFactor * o.MaxSpeed, (o.Target - o.Pos).FlatLength * 3f);

            if (speed < 1.4f && desiredSpeed < 1.6f || o.Shielding)
            {
                // Close control / shielding: keep the ball glued softly under the body's far foot.
                Vec3 keep = o.Shielding ? o.Pos + o.FacingDir * -0.05f + (o.Pos - (NearestOpponent(o)?.Pos ?? o.Pos)).FlatNormalized * 0.45f : foot;
                Ball.Position = Vec3.Lerp(Ball.Position, new Vec3(keep.X, BallPhysics.Radius, keep.Z), MathUtil.Clamp01(6f * dt));
                Ball.Velocity = o.Vel;
                return;
            }

            if (SimTime >= o.NextTouchTime && d < 1.5f && Ball.Position.Y < 0.5f)
            {
                float ctrl = MathUtil.Attr(o.A.Dribbling, 0f, 1f);
                if (o.Profile.HasTrait(Traits.CloseControl)) ctrl = MathF.Min(1f, ctrl + 0.2f);
                float sNorm = MathUtil.Clamp01(speed / 9f);
                float extra = MathUtil.Lerp(1.0f, 3.2f, sNorm) * (1.15f - 0.35f * ctrl);
                float ballSpeed = MathF.Max(speed, desiredSpeed) + extra;
                Ball.Velocity = dir * ballSpeed;
                Ball.Position = Vec3.Lerp(Ball.Position, new Vec3(foot.X, BallPhysics.Radius, foot.Z), 0.5f);
                o.NextTouchTime = SimTime + MathUtil.Lerp(0.5f, 0.3f, sNorm) * (1.1f - 0.2f * ctrl);
                TrajectoryVersion++;
                Emit(MatchEventType.BallTouch, o.Team, o, Ball.Position, ballSpeed);
            }
        }

        /// <summary>Contacts between players and a ball that nobody controls (or a loosely controlled one).</summary>
        private void CheckLooseContacts()
        {
            if (Holder != null || Phase != MatchPhase.Playing) return;
            _contactCandidates.Clear();
            foreach (var p in Players)
            {
                if (!p.IsActive || p.Stun > 0f || p.TouchCooldown > 0f || p == Owner) continue;
                if (p.Action == PlayerAction.Fall || p.Action == PlayerAction.Slide || p.Action == PlayerAction.KeeperDive) continue;
                if (IsKickAction(p.Action) && !p.ContactDone) continue;
                _contactCandidates.Add(p);
            }
            if (_contactCandidates.Count == 0) return;
            _contactCandidates.Sort((a, b) => Vec3.FlatDistance(a.Pos, Ball.Position).CompareTo(Vec3.FlatDistance(b.Pos, Ball.Position)));

            foreach (var p in _contactCandidates)
            {
                // Closest point of this step's ball segment to the body (handles fast balls).
                float t = MathUtil.ClosestFlatT(BallPrevPos, Ball.Position, p.Pos);
                Vec3 bp = Vec3.Lerp(BallPrevPos, Ball.Position, t);
                float flat = Vec3.FlatDistance(bp, p.Pos);
                float reach = p.FootReach;
                if (p.Profile.HasTrait(Traits.Interceptor)) reach += 0.12f;
                float headTop = 1.85f + p.Profile.HeightCm / 100f * 0.1f - 0.18f + p.JumpHeight + (p.Profile.HasTrait(Traits.AerialThreat) ? 0.15f : 0f);
                bool keeperHands = p.IsGoalkeeper && Pitch.IsInsidePenaltyArea(p.Pos, -AttackDir(p.Team)) && !IsBackPassTo(p);
                if (keeperHands) { reach = 1.05f; headTop = 2.45f + p.JumpHeight; }
                if (flat > reach || bp.Y > headTop) continue;

                if (Owner != null)
                {
                    if (Owner.Team == p.Team) continue;
                    // Poke / steal a loosely controlled ball.
                    float ownerDist = Vec3.FlatDistance(Ball.Position, Owner.Pos);
                    if (ownerDist < 0.75f || flat > ownerDist) continue;
                    p.TouchCooldown = 0.45f;
                    p.Stats.Tackles++; Teams[p.Team].Stats.Tackles++;
                    float steal = 0.22f + MathUtil.Attr(p.A.Interceptions, 0f, 0.3f) - MathUtil.Attr(Owner.A.Dribbling, 0f, 0.18f);
                    if (!Rng.Chance(steal)) continue;
                    var victim = Owner;
                    Owner = null;
                    Emit(MatchEventType.TackleWon, p.Team, p, Ball.Position, 0, victim);
                    p.Stats.TacklesWon++; Teams[p.Team].Stats.TacklesWon++;
                    ResolveLooseContact(p, bp);
                    return;
                }
                if (ResolveLooseContact(p, bp)) return;
            }
        }

        private bool IsBackPassTo(MatchPlayer gk) =>
            LastKicker != null && LastKicker.Team == gk.Team && LastKicker != gk && LastTouch == LastKicker &&
            (LastKickKind == KickKind.GroundPass || LastKickKind == KickKind.ThroughPass || LastKickKind == KickKind.Lofted);

        /// <summary>Returns true if the player touched the ball.</summary>
        private bool ResolveLooseContact(MatchPlayer p, Vec3 contactPoint)
        {
            Vec3 rel = Ball.Velocity - p.Vel;
            float relSpeed = rel.Length;
            float h = contactPoint.Y;
            int defSide = -AttackDir(p.Team);
            bool keeperHands = p.IsGoalkeeper && Pitch.IsInsidePenaltyArea(p.Pos, defSide) && !IsBackPassTo(p);

            if (keeperHands)
            {
                float catchP = MathUtil.Attr(p.A.GkHandling, 0.55f, 0.97f) * MathUtil.Clamp(1.3f - relSpeed / 30f, 0.3f, 1f);
                bool shotTowardsGoal = _activeShot && _activeShooter != null && _activeShooter.Team != p.Team;
                if (Rng.Chance(catchP))
                {
                    if (OnTouch(p, true)) return true;
                    if (shotTowardsGoal) RegisterSave(p);
                    Holder = p;
                    Owner = null;
                    Ball.Held = true;
                    p.StartAction(PlayerAction.KeeperCatch, 0.6f, 99f);
                    p.HoldTimer = Rng.Range(1.2f, 2.4f);
                    Emit(MatchEventType.KeeperClaim, p.Team, p, Ball.Position, 0);
                    SyncHeldBall();
                    return true;
                }
                Parry(p, contactPoint, relSpeed);
                if (shotTowardsGoal) RegisterSave(p);
                return true;
            }

            // User or AI first-time action (header, volley, first-time pass/shot)
            PendingKick ft;
            bool haveFirstTime = false;
            if (p.UserControlled && !UserAutopilot && p.QueuedFirstTimeTimer > 0f) { ft = p.QueuedFirstTime; haveFirstTime = true; p.QueuedFirstTimeTimer = 0; }
            else haveFirstTime = Ai.TryFirstTimeAction(p, contactPoint, out ft);

            if (haveFirstTime)
            {
                if (OnTouch(p, true)) return true;
                if (h > 1.1f) ft.Kind = KickKind.Header;
                else if (ft.Kind == KickKind.Shot || ft.Kind == KickKind.Volley) ft.Kind = h > 0.45f ? KickKind.Volley : KickKind.Shot;
                if (ft.Kind == KickKind.Header || ft.Kind == KickKind.Volley)
                {
                    p.Facing = (ft.Target - p.Pos).FlatNormalized.FlatAngle;
                    Owner = null;
                    StartKick(p, ft);
                }
                else
                {
                    // Ground first-time kick: execute immediately at this contact.
                    Owner = p;
                    p.Kick = ft;
                    p.StartAction(ft.IsShot ? PlayerAction.Shot : PlayerAction.Pass, 0.4f, 0f);
                    p.ContactDone = true;
                    Ball.Position = new Vec3(contactPoint.X, BallPhysics.Radius, contactPoint.Z);
                    ExecuteKick(p);
                }
                return true;
            }

            if (h > 1.75f + p.JumpHeight)
            {
                // Too high to control without a header decision: it glances off the head or passes over.
                if (!p.WantsHeader) return false;
                if (OnTouch(p, true)) return true;
                Ball.Velocity = (Ball.Velocity * -0.25f + new Vec3(0, 2.5f, 0)).RotateY(Rng.Range(-1f, 1f));
                p.TouchCooldown = 0.3f;
                return true;
            }

            // Control attempt: harder for fast balls, high balls, under pressure.
            float skill = MathUtil.Attr(p.A.BallControl, 13f, 30f);
            float difficulty = relSpeed / skill + (h > 0.7f ? 0.3f : 0f) + (h > 1.3f ? 0.25f : 0f);
            float pressure = MathUtil.Clamp01((1.8f - NearestOpponentDistance(p)) / 1.8f) * 0.2f;
            bool intended = _pendingPass && _passReceiverId == p.Id;
            float success = MathUtil.Clamp(1.15f - difficulty - pressure + (intended ? 0.1f : 0f), 0.12f, 0.98f);

            if (OnTouch(p, true)) return true;
            if (Rng.Chance(success))
            {
                Owner = p;
                Vec3 dir = p.DribbleDir.SqrLength > 0.5f ? p.DribbleDir : p.FacingDir;
                Ball.Velocity = dir * MathF.Max(1.5f, p.Speed * 0.95f);
                Ball.Position = new Vec3(contactPoint.X, MathF.Max(BallPhysics.Radius, MathF.Min(h, 0.5f)), contactPoint.Z);
                if (Ball.Position.Y > BallPhysics.Radius) Ball.Velocity = new Vec3(Ball.Velocity.X, -1f, Ball.Velocity.Z);
                p.NextTouchTime = SimTime + 0.18f;
                if (h > 0.75f) p.StartAction(PlayerAction.Trap, 0.35f, 99f);
                TrajectoryVersion++;
                Emit(MatchEventType.BallTouch, p.Team, p, Ball.Position, relSpeed);
            }
            else
            {
                // Heavy touch / deflection.
                Vec3 outDir = (Ball.Velocity.FlatNormalized * -0.3f + p.FacingDir * 0.6f).FlatNormalized.RotateY(Rng.Range(-1.2f, 1.2f));
                float s = MathUtil.Clamp(relSpeed * Rng.Range(0.25f, 0.5f), 1.5f, 9f);
                Ball.Velocity = outDir * s + new Vec3(0, h > 0.5f ? Rng.Range(0.5f, 2.5f) : 0f, 0);
                p.TouchCooldown = 0.3f;
                TrajectoryVersion++;
                Emit(MatchEventType.BallTouch, p.Team, p, Ball.Position, relSpeed);
            }
            return true;
        }

        /// <summary>Common bookkeeping for every touch. Returns true if the touch stopped play (offside).</summary>
        private bool OnTouch(MatchPlayer p, bool deliberate)
        {
            if (_offsideTeam >= 0)
            {
                if (_offsideTeam == p.Team && _offsideIds.Contains(p.Id))
                {
                    var spot = p.Pos;
                    ClearOffside();
                    _pendingPass = false;
                    p.Stats.Offsides++;
                    Teams[p.Team].Stats.Offsides++;
                    Emit(MatchEventType.Offside, p.Team, p, spot, 0);
                    LastTouch = p;
                    BeginStoppage(new Restart { Type = RestartType.FreeKick, Team = 1 - p.Team, Spot = Pitch.ClampToField(spot, 1f), Indirect = true }, 1.8f);
                    return true;
                }
                ClearOffside();
            }

            if (_pendingPass && _passFrom != null && p != _passFrom)
            {
                if (p.Team == _passFrom.Team)
                {
                    if (LastKickKind != KickKind.Throw)
                    {
                        _passFrom.Stats.PassesCompleted++;
                        Teams[p.Team].Stats.PassesCompleted++;
                    }
                    _lastCompletedPassFrom = _passFrom;
                    _lastCompletedPassTo = p;
                    _lastCompletedPassTime = SimTime;
                    Emit(MatchEventType.PassCompleted, p.Team, _passFrom, p.Pos, 0, p);
                }
                else
                {
                    p.Stats.Interceptions++;
                    Teams[p.Team].Stats.Interceptions++;
                    Emit(MatchEventType.Interception, p.Team, p, p.Pos, 0, _passFrom);
                }
                _pendingPass = false;
            }
            if (_activeShot && p != _activeShooter) _activeShot = false;
            if (LastTouch != null && LastTouch.Team != p.Team) _lastCompletedPassTo = null;
            LastTouch = p;
            return false;
        }

        // ------------------------------------------------------------------ goalkeeper saves

        /// <summary>Starts a dive towards `dir` for the keeper (called by the goalkeeper AI).</summary>
        internal void StartDive(MatchPlayer gk, Vec3 divePoint, float speed, float duration, float jump)
        {
            gk.Target = divePoint;
            gk.ActionDir = (divePoint - gk.Pos).FlatNormalized;
            gk.ActionSpeed = speed;
            gk.StartAction(PlayerAction.KeeperDive, duration, 99f);
            if (jump > 0f) gk.JumpVel = jump;
        }

        private void CheckKeeperSaves()
        {
            if (Holder != null || Owner != null) return;
            for (int team = 0; team < 2; team++)
            {
                var gk = Teams[team].Goalkeeper;
                if (gk == null || gk.Stun > 0.3f && gk.Action != PlayerAction.KeeperDive) continue;
                if (LastTouch != null && LastTouch.Team == team) continue;
                if (gk.TouchCooldown > 0f) continue;
                int side = -AttackDir(team);
                if (Ball.Velocity.X * side <= 0.5f) continue;
                if (!Pitch.IsInsidePenaltyArea(gk.Pos, side) && MathF.Abs(gk.Pos.X) < Pitch.HalfLength - 20f) continue;

                float plane = gk.Pos.X;
                bool crossed = (BallPrevPos.X - plane) * side < 0 && (Ball.Position.X - plane) * side >= 0;
                if (!crossed) continue;
                float t = MathUtil.InverseLerp(BallPrevPos.X * side, Ball.Position.X * side, plane * side);
                Vec3 c = Vec3.Lerp(BallPrevPos, Ball.Position, t);
                bool diving = gk.Action == PlayerAction.KeeperDive;
                float lateral = MathF.Abs(c.Z - gk.Pos.Z);
                float reach = diving ? MathUtil.Attr(gk.A.GkDiving, 0.95f, 1.3f) : 0.55f + MathUtil.Attr(gk.A.GkReflexes, 0f, 0.25f);
                if (gk.Profile.HasTrait(Traits.ShotStopper)) reach += 0.1f;
                float top = 2.05f + gk.JumpHeight + (diving ? 0.35f : 0.25f);
                if (lateral > reach || c.Y > top) continue;

                float speed = Ball.Velocity.Length;
                bool onTarget = MathF.Abs(c.Z) < Pitch.HalfGoalWidth + 0.3f && c.Y < Pitch.GoalHeight + 0.2f;
                // Balls at the edge of reach are harder to hold.
                float edge = MathUtil.Clamp01(lateral / reach);
                // Low balls far from the body and fast balls at full stretch may only be fingertipped.
                float lowPenalty = c.Y < 0.45f && lateral > 0.6f ? 0.15f : 0f;
                float touchP = MathUtil.Clamp01(1.25f - edge * 0.55f - lowPenalty - MathF.Max(0f, speed - 22f) * 0.02f) * MathUtil.Attr(gk.A.GkReflexes, 0.8f, 1.05f);
                if (!Rng.Chance(touchP))
                {
                    if (edge > 0.75f && Rng.Chance(0.5f))
                    {
                        // Fingertip touch that does not stop the ball.
                        Ball.Velocity = Ball.Velocity * 0.82f + new Vec3(0, 0, MathF.Sign(c.Z - gk.Pos.Z) * 1.2f);
                        if (OnTouch(gk, false)) return;
                        gk.TouchCooldown = 0.6f;
                    }
                    else gk.TouchCooldown = 0.6f;
                    continue;
                }
                float catchP = MathUtil.Attr(gk.A.GkHandling, 0.25f, 0.85f) * MathUtil.Clamp(1.35f - speed / 30f, 0.2f, 1f) * (1.15f - 0.75f * edge);
                if (OnTouch(gk, true)) return;
                if (Rng.Chance(catchP))
                {
                    Holder = gk;
                    Ball.Held = true;
                    if (!diving) gk.StartAction(PlayerAction.KeeperCatch, 0.6f, 99f);
                    gk.HoldTimer = Rng.Range(1.4f, 2.6f);
                    SyncHeldBall();
                }
                else Parry(gk, c, speed);
                if (onTarget || _activeShot) RegisterSave(gk);
                return;
            }
        }

        private void Parry(MatchPlayer gk, Vec3 at, float speed)
        {
            int side = -AttackDir(gk.Team);
            float outward = MathF.Sign(at.Z == 0 ? Rng.Range(-1f, 1f) : at.Z);
            Vec3 dir = new Vec3(-side * Rng.Range(0.3f, 1f), 0, outward * Rng.Range(0.5f, 1.2f)).FlatNormalized;
            float s = speed * Rng.Range(0.25f, 0.45f);
            Ball.Position = new Vec3(at.X - side * 0.15f, MathF.Max(BallPhysics.Radius, at.Y), at.Z);
            Ball.Velocity = dir * s + new Vec3(0, Rng.Range(1f, 4.5f), 0);
            gk.TouchCooldown = 0.5f;
            TrajectoryVersion++;
        }

        private void RegisterSave(MatchPlayer gk)
        {
            gk.Stats.Saves++;
            Teams[gk.Team].Stats.Saves++;
            Emit(MatchEventType.Save, gk.Team, gk, Ball.Position, _activeShotXg, _activeShooter);
            _activeShot = false;
        }

        /// <summary>Called by the goalkeeper AI when it decides to distribute the ball.</summary>
        internal bool KeeperRelease(MatchPlayer gk, PendingKick k)
        {
            if (Holder != gk) return false;
            gk.ClearAction();
            return StartKick(gk, k);
        }

        // ------------------------------------------------------------------ offside

        private void ClearOffside()
        {
            _offsideIds.Clear();
            _offsideTeam = -1;
        }

        private void SnapshotOffside(MatchPlayer kicker)
        {
            ClearOffside();
            if (!Settings.OffsideEnabled) return;
            int team = kicker.Team;
            float line = SecondLastDefenderAttackX(team);
            float ballAx = AttackX(team, Ball.Position);
            foreach (var m in Teams[team].Players)
            {
                if (!m.IsActive || m == kicker) continue;
                float ax = AttackX(team, m.Pos);
                if (ax > 0f && ax > line + 0.15f && ax > ballAx + 0.15f) _offsideIds.Add(m.Id);
            }
            if (_offsideIds.Count > 0) _offsideTeam = team;
        }

        /// <summary>Attacking-frame X of the second-last opponent (offside line) for `team`.</summary>
        public float SecondLastDefenderAttackX(int team)
        {
            float first = -999f, second = -999f;
            foreach (var d in Teams[1 - team].Players)
            {
                if (!d.IsActive) continue;
                float ax = AttackX(team, d.Pos);
                if (ax > first) { second = first; first = ax; }
                else if (ax > second) second = ax;
            }
            return MathF.Max(second, 0f);
        }

        // ------------------------------------------------------------------ helpers

        public MatchPlayer NearestOpponent(MatchPlayer p)
        {
            MatchPlayer best = null;
            float bd = float.MaxValue;
            foreach (var o in Teams[1 - p.Team].Players)
            {
                if (!o.IsActive) continue;
                float d = (o.Pos - p.Pos).FlatXZ.SqrLength;
                if (d < bd) { bd = d; best = o; }
            }
            return best;
        }

        public float NearestOpponentDistance(MatchPlayer p)
        {
            var o = NearestOpponent(p);
            return o == null ? 99f : Vec3.FlatDistance(o.Pos, p.Pos);
        }
    }
}
