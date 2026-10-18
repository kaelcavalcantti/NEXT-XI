using System;

namespace NextXI.Core.Match
{
    /// <summary>Context-sensitive interpretation of the user's touch commands.</summary>
    public sealed partial class MatchEngine
    {
        public void SwitchUserTo(MatchPlayer p)
        {
            if (p == null || p.Team != UserTeam || !p.IsActive || p == UserPlayer) return;
            if (Settings.ControlMode == ControlMode.PlayerLock) return;
            if (UserPlayer != null) UserPlayer.UserControlled = false;
            UserPlayer = p;
            p.UserControlled = true;
        }

        public void SwitchUserToBest()
        {
            if (UserTeam < 0) return;
            MatchPlayer best = null;
            float bd = float.MaxValue;
            Vec3 ball = Ball.Position + Ball.Velocity * 0.3f;
            foreach (var p in Teams[UserTeam].Players)
            {
                if (!p.IsActive || p.IsGoalkeeper || p == UserPlayer) continue;
                float d = Vec3.FlatDistance(p.Pos, ball);
                // Prefer players goal-side of the ball when defending.
                if (AttackX(UserTeam, p.Pos) > AttackX(UserTeam, ball)) d += 4f;
                if (d < bd) { bd = d; best = p; }
            }
            if (best != null) SwitchUserTo(best);
        }

        private void ApplyUserCommand(float dt)
        {
            var u = UserPlayer;
            if (u == null) return;

            if (Settings.ControlMode == ControlMode.TeamSwitching)
            {
                if (Owner != null && Owner.Team == UserTeam && Owner != u) { SwitchUserTo(Owner); u = UserPlayer; }
                else if (Holder != null && Holder.Team == UserTeam && Holder != u && Holder.IsGoalkeeper) { /* keeper distributes by AI */ }
                else if (PassInFlight && PassReceiverId >= 0)
                {
                    var r = FindPlayer(PassReceiverId);
                    if (r != null && r.Team == UserTeam && r != u) { SwitchUserTo(r); u = UserPlayer; }
                }
            }
            if (!u.IsActive) return;
            if (u.IsGoalkeeper) { ApplyUserKeeper(u); return; }

            // ---- movement
            Vec3 move = Command.Move.FlatXZ.ClampLength(1f);
            float mag = move.Length;
            bool hasBall = Owner == u;
            if (mag > 0.08f)
            {
                Vec3 md = move / mag;
                u.Target = u.Pos + md * 4f;
                float walk = hasBall ? 0.62f : 0.7f;
                u.TargetSpeedFactor = MathUtil.Clamp01(mag) * (Command.Sprint ? 1f : walk);
                u.DribbleDir = md;
            }
            else
            {
                u.Target = u.Pos + u.Vel * 0.15f;
                u.TargetSpeedFactor = 0f;
                if (hasBall) u.DribbleDir = u.FacingDir;
            }
            u.Shielding = hasBall && Command.ShieldHeld;
            u.Jockeying = !hasBall && Command.JockeyHeld && PossessionTeam != UserTeam;

            if (!Command.AnyAction || !u.CanAct && !(Command.Switch)) return;

            Vec3 aim = mag > 0.2f ? move / mag : u.FacingDir;
            switch (CurrentUserContext)
            {
                case UserContext.Attacking:
                    UserAttack(u, aim, mag > 0.2f);
                    break;
                case UserContext.SupportingAttack:
                    if (Command.Pass) u.CallForPassTimer = 2.0f;
                    if (Command.Through) { u.CallForThroughTimer = 2.5f; }
                    if (Owner == null && (Command.Pass || Command.Shoot || Command.Lob))
                        QueueFirstTime(u, aim, mag > 0.2f);
                    break;
                case UserContext.Defending:
                    if (Command.Switch || Command.Lob) { SwitchUserToBest(); break; }
                    if (Command.Pass)
                    {
                        if (Owner == null && Vec3.FlatDistance(u.Pos, Ball.Position) < 4f) QueueFirstTime(u, aim, mag > 0.2f);
                        else StartTackle(u);
                    }
                    else if (Command.Shoot)
                    {
                        Vec3 dir = mag > 0.2f ? aim : (Ball.Position + Ball.Velocity * 0.2f - u.Pos).FlatNormalized;
                        StartSlide(u, dir);
                    }
                    else if (Command.Through) RequestTeammatePress();
                    break;
            }
        }

        /// <summary>
        /// Assisted goalkeeper: the keeper AI positions, dives and claims; the stick moves the keeper
        /// (e.g. to come off the line) when no shot is travelling, and the buttons distribute the ball.
        /// </summary>
        private void ApplyUserKeeper(MatchPlayer u)
        {
            Vec3 move = Command.Move.FlatXZ.ClampLength(1f);
            float mag = move.Length;
            Vec3 aim = mag > 0.2f ? move / mag : new Vec3(AttackDir(u.Team), 0, 0);
            if (Holder == u && u.Action == PlayerAction.None || u.Action == PlayerAction.KeeperCatch && Holder == u)
            {
                if (Command.Pass)
                {
                    var r = ChooseUserReceiver(u, aim, 40f);
                    KeeperRelease(u, new PendingKick { Kind = KickKind.KeeperThrow, Target = r?.Pos ?? u.Pos + aim * 15f, ReceiverId = r?.Id ?? -1, Power = 0.6f, ErrorScale = 0.9f, UserInitiated = true });
                }
                else if (Command.Shoot || Command.Lob || Command.Through)
                {
                    var r = ChooseUserReceiver(u, aim, 75f, true);
                    KeeperRelease(u, new PendingKick { Kind = KickKind.KeeperKick, Target = r?.Pos ?? u.Pos + aim * 50f, ReceiverId = r?.Id ?? -1, Power = 1f, ErrorScale = 1f, UserInitiated = true });
                }
                return;
            }
            if (Owner == u) { UserAttack(u, aim, mag > 0.2f); if (mag > 0.08f) { u.Target = u.Pos + move.FlatNormalized * 3f; u.TargetSpeedFactor = mag * 0.7f; u.DribbleDir = move.FlatNormalized; } return; }
            if (u.IsBusy || ShotInFlight) return;
            if (mag > 0.2f)
            {
                u.Target = u.Pos + move / mag * 3f;
                u.TargetSpeedFactor = mag * (Command.Sprint ? 1f : 0.7f);
            }
            if (Command.Shoot && Owner != null && Owner.Team != u.Team && Vec3.FlatDistance(Owner.Pos, u.Pos) < 4f) StartSlide(u, (Ball.Position - u.Pos).FlatNormalized);
        }

        private void QueueFirstTime(MatchPlayer u, Vec3 aim, bool aimed)
        {
            PendingKick k;
            if (Command.Shoot) k = BuildUserShot(u, aim, aimed, Command.Power);
            else if (Command.Lob)
            {
                var r = ChooseUserReceiver(u, aim, 45f);
                k = new PendingKick { Kind = KickKind.Lofted, Target = r?.Pos ?? u.Pos + aim * 20f, ReceiverId = r?.Id ?? -1, Power = 0.6f, ErrorScale = 1f, UserInitiated = true };
            }
            else
            {
                var r = ChooseUserReceiver(u, aim, 35f);
                k = new PendingKick { Kind = KickKind.GroundPass, Target = r?.Pos ?? u.Pos + aim * 12f, ReceiverId = r?.Id ?? -1, Power = 0.5f, ErrorScale = 1f, UserInitiated = true };
            }
            u.QueuedFirstTime = k;
            u.QueuedFirstTimeTimer = 1.0f;
        }

        private void UserAttack(MatchPlayer u, Vec3 aim, bool aimed)
        {
            float assist = Settings.PassAssist;
            if (Command.Shoot)
            {
                StartKick(u, BuildUserShot(u, aim, aimed, Command.Power));
                return;
            }
            if (Command.Pass)
            {
                var r = ChooseUserReceiver(u, aim, 40f);
                Vec3 target = r != null ? r.Pos : u.Pos + aim * MathUtil.Lerp(8f, 25f, Command.Power);
                if (r != null && assist < 1f)
                {
                    // Low assistance: direction follows the stick more than the teammate.
                    float d = Vec3.FlatDistance(u.Pos, r.Pos);
                    Vec3 manual = u.Pos + aim * d;
                    target = Vec3.Lerp(manual, r.Pos, assist);
                }
                StartKick(u, new PendingKick
                {
                    Kind = KickKind.GroundPass, Target = target, ReceiverId = assist > 0.5f ? (r?.Id ?? -1) : -1,
                    Power = MathUtil.Clamp01(0.3f + Command.Power * 0.6f), ErrorScale = MathUtil.Lerp(1.25f, 0.9f, assist), UserInitiated = true
                });
                return;
            }
            if (Command.Through)
            {
                var r = ChooseUserReceiver(u, aim, 45f, preferRunners: true);
                int dir = AttackDir(u.Team);
                Vec3 lead;
                if (r != null)
                {
                    Vec3 runDir = r.Vel.FlatLength > 2f ? r.Vel.FlatNormalized : new Vec3(dir, 0, 0);
                    lead = r.Pos + r.Vel * 0.9f + runDir * MathUtil.Lerp(3f, 9f, Command.Power);
                }
                else lead = u.Pos + aim * MathUtil.Lerp(12f, 30f, Command.Power);
                StartKick(u, new PendingKick { Kind = KickKind.ThroughPass, Target = lead, ReceiverId = r?.Id ?? -1, Power = 0.5f, ErrorScale = MathUtil.Lerp(1.2f, 0.9f, assist), UserInitiated = true });
                return;
            }
            if (Command.Lob)
            {
                int dir = AttackDir(u.Team);
                bool crossZone = AttackX(u.Team, u.Pos) > Pitch.HalfLength - 30f && MathF.Abs(u.Pos.Z) > 11f;
                if (crossZone)
                {
                    float zs = MathF.Sign(u.Pos.Z);
                    // Stick towards goal = near post, away = far post, neutral = penalty spot.
                    float aimZ = aimed ? MathUtil.Clamp(-aim.Z * zs, -1f, 1f) : 0f;
                    Vec3 t = new Vec3((Pitch.HalfLength - MathUtil.Lerp(10f, 6f, MathF.Abs(aimZ))) * dir, 0, aimZ > 0.3f ? zs * 2f : (aimZ < -0.3f ? -zs * 4f : 0f));
                    var r = NearestTeammate(u, t);
                    StartKick(u, new PendingKick { Kind = KickKind.Cross, Target = t, ReceiverId = r?.Id ?? -1, Power = Command.Power, ErrorScale = MathUtil.Lerp(1.2f, 0.9f, assist), Curl = -zs * dir * 0.5f, UserInitiated = true });
                }
                else
                {
                    var r = ChooseUserReceiver(u, aim, 60f);
                    Vec3 t = r != null ? r.Pos + r.Vel * 1.2f : u.Pos + aim * MathUtil.Lerp(18f, 45f, Command.Power);
                    StartKick(u, new PendingKick { Kind = KickKind.Lofted, Target = t, ReceiverId = r?.Id ?? -1, Power = 0.6f, ErrorScale = MathUtil.Lerp(1.2f, 0.9f, assist), UserInitiated = true });
                }
                return;
            }
            if (Command.Skill) UserSkillMove(u, aim, aimed);
        }

        private void UserSkillMove(MatchPlayer u, Vec3 aim, bool aimed)
        {
            Vec3 forward = u.Speed > 1f ? u.Vel.FlatNormalized : u.FacingDir;
            float side = aimed ? Vec3.Dot(aim, forward.RotateY(MathUtil.Pi * 0.5f)) : 0f;
            u.StartAction(PlayerAction.Skill, 0.45f, 99f);
            if (MathF.Abs(side) < 0.5f)
            {
                // Knock-on: push the ball ahead into space and burst after it.
                float push = MathUtil.Attr(u.A.Dribbling, 5.5f, 8f) + (u.Profile.HasTrait(Data.Traits.Explosive) ? 1.5f : 0f);
                Ball.Velocity = (aimed ? aim : forward) * (u.Speed + push);
                u.NextTouchTime = SimTime + 0.6f;
                u.TargetSpeedFactor = 1f;
                u.Target = u.Pos + (aimed ? aim : forward) * 10f;
            }
            else
            {
                // Feint / drag-back: sharp lateral shift of the ball, harder to read for the defender.
                Vec3 lateral = forward.RotateY(MathUtil.Pi * 0.5f * MathF.Sign(side));
                Ball.Velocity = lateral * MathUtil.Attr(u.A.Agility, 3f, 4.6f) + forward * 1f;
                u.NextTouchTime = SimTime + 0.35f;
                var opp = NearestOpponent(u);
                if (opp != null && Vec3.FlatDistance(opp.Pos, u.Pos) < 2.5f && Rng.Chance(MathUtil.Attr(u.A.Dribbling, 0.25f, 0.65f)))
                {
                    opp.Stun = MathF.Max(opp.Stun, 0.35f); // defender wrong-footed
                    u.Stats.DribblesWon++;
                }
            }
            Emit(MatchEventType.Dribble, u.Team, u, Ball.Position, 0);
        }

        private PendingKick BuildUserShot(MatchPlayer u, Vec3 aim, bool aimed, float power)
        {
            int side = AttackDir(u.Team);
            float hw = Pitch.HalfGoalWidth;
            var gk = Teams[1 - u.Team].Goalkeeper;
            float gkZ = gk?.Pos.Z ?? 0f;
            float idealZ = (gkZ > u.Pos.Z * 0.15f ? -1f : 1f) * (hw - 0.55f);
            float manualZ = idealZ;
            if (aimed)
            {
                // Project the stick direction onto the goal line.
                float lineX = side * Pitch.HalfLength;
                if (aim.X * side > 0.15f)
                    manualZ = u.Pos.Z + aim.Z / aim.X * (lineX - u.Pos.X);
                else
                    manualZ = MathF.Sign(aim.Z == 0 ? idealZ : aim.Z) * hw;
                manualZ = MathUtil.Clamp(manualZ, -(hw - 0.3f), hw - 0.3f);
            }
            float z = MathUtil.Lerp(manualZ, idealZ, Settings.ShotAssist * (aimed ? 0.6f : 1f));
            float y = MathUtil.Lerp(0.25f, 2.1f, MathUtil.Clamp01((power - 0.2f) / 0.8f));
            return new PendingKick
            {
                Kind = KickKind.Shot,
                Target = new Vec3(side * (Pitch.HalfLength + 0.3f), y, z),
                Power = MathUtil.Clamp01(power <= 0f ? 0.6f : power),
                IsShot = true,
                ReceiverId = -1,
                ErrorScale = MathUtil.Lerp(1.3f, 0.85f, Settings.ShotAssist),
                UserInitiated = true
            };
        }

        /// <summary>Best teammate in the direction of `aim` (cone-weighted, distance-penalised).</summary>
        public MatchPlayer ChooseUserReceiver(MatchPlayer u, Vec3 aim, float maxDist, bool preferRunners = false)
        {
            MatchPlayer best = null;
            float bs = float.MinValue;
            foreach (var r in Teams[u.Team].Players)
            {
                if (!r.IsActive || r == u) continue;
                Vec3 to = (r.Pos - u.Pos).FlatXZ;
                float d = to.Length;
                if (d < 2.5f || d > maxDist) continue;
                float cos = Vec3.Dot(to / d, aim);
                if (cos < 0.35f) continue;
                float s = cos * 3f - d / 25f;
                if (preferRunners && Vec3.Dot(r.Vel, new Vec3(AttackDir(u.Team), 0, 0)) > 3f) s += 0.6f;
                if (r.IsGoalkeeper) s -= 1f;
                if (s > bs) { bs = s; best = r; }
            }
            return best;
        }

        private MatchPlayer NearestTeammate(MatchPlayer u, Vec3 point)
        {
            MatchPlayer best = null; float bd = float.MaxValue;
            foreach (var r in Teams[u.Team].Players)
            {
                if (!r.IsActive || r == u || r.IsGoalkeeper) continue;
                float d = Vec3.FlatDistance(r.Pos, point);
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        private float _teammatePressTimer;
        public bool TeammatePressActive => _teammatePressTimer > SimTime;

        private void RequestTeammatePress() => _teammatePressTimer = SimTime + 2.5f;

        private void ApplyUserSetPiece(MatchPlayer taker)
        {
            if (!Command.AnyAction) return;
            Vec3 move = Command.Move.FlatXZ;
            bool aimed = move.Length > 0.2f;
            Vec3 aim = aimed ? move.Normalized : taker.FacingDir;
            var r = CurrentRestart;
            if (r.Type == RestartType.ThrowIn)
            {
                var mate = ChooseUserReceiver(taker, aim, 25f);
                Vec3 t = mate?.Pos ?? taker.Pos + aim * MathUtil.Lerp(6f, 18f, Command.Power);
                StartKick(taker, new PendingKick { Kind = KickKind.Throw, Target = t, ReceiverId = mate?.Id ?? -1, Power = 0.5f, ErrorScale = 1f, UserInitiated = true });
                return;
            }
            if (Command.Shoot && (r.Type == RestartType.Penalty || r.Type == RestartType.FreeKick))
            {
                var k = BuildUserShot(taker, aim, aimed, Command.Power);
                if (r.Type == RestartType.FreeKick) k.Curl = -MathF.Sign(k.Target.Z) * AttackDir(taker.Team);
                StartKick(taker, k);
                return;
            }
            if (Command.Lob || (r.Type == RestartType.Corner && !Command.Pass))
            {
                int dir = AttackDir(taker.Team);
                float zs = MathF.Sign(taker.Pos.Z == 0 ? 1 : taker.Pos.Z);
                Vec3 t;
                if (r.Type == RestartType.Corner || AttackX(taker.Team, taker.Pos) > Pitch.HalfLength - 35f)
                {
                    float aimZ = aimed ? MathUtil.Clamp(-aim.Z * zs, -1f, 1f) : 0f;
                    t = new Vec3((Pitch.HalfLength - MathUtil.Lerp(10f, 6f, MathF.Abs(aimZ))) * dir, 0, aimZ > 0.3f ? zs * 2f : (aimZ < -0.3f ? -zs * 4f : 0f));
                    var mate = NearestTeammate(taker, t);
                    StartKick(taker, new PendingKick { Kind = KickKind.Cross, Target = t, ReceiverId = mate?.Id ?? -1, Power = Command.Power, ErrorScale = 1f, Curl = -zs * dir, UserInitiated = true });
                }
                else
                {
                    var mate = ChooseUserReceiver(taker, aim, 60f);
                    t = mate?.Pos ?? taker.Pos + aim * 30f;
                    StartKick(taker, new PendingKick { Kind = KickKind.Lofted, Target = t, ReceiverId = mate?.Id ?? -1, Power = 0.6f, ErrorScale = 1f, UserInitiated = true });
                }
                return;
            }
            {
                var mate = ChooseUserReceiver(taker, aim, 40f);
                Vec3 t = mate?.Pos ?? taker.Pos + aim * 12f;
                StartKick(taker, new PendingKick { Kind = Command.Through ? KickKind.ThroughPass : KickKind.GroundPass, Target = t, ReceiverId = mate?.Id ?? -1, Power = 0.5f, ErrorScale = 1f, UserInitiated = true });
            }
        }
    }
}
