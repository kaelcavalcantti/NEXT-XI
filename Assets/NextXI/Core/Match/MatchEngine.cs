using System;
using System.Collections.Generic;
using NextXI.Core.Data;

namespace NextXI.Core.Match
{
    public enum MatchPhase { PreMatch, Playing, Stoppage, SetPiece, GoalScored, HalfTime, FullTime }

    public enum RestartType { KickOff, ThrowIn, Corner, GoalKick, FreeKick, Penalty, DropBall }

    [Serializable]
    public struct Restart
    {
        public RestartType Type;
        /// <summary>Team taking the restart.</summary>
        public int Team;
        public Vec3 Spot;
        public int TakerId;
        public bool Indirect;
    }

    /// <summary>
    /// Continuous 11v11 football simulation with a fixed 60 Hz step. Owns the ball,
    /// players, referee and clock. Presentation layers read state and drain events;
    /// the user writes PlayerCommand. No engine dependency: runs headless in tests.
    /// </summary>
    public sealed partial class MatchEngine
    {
        public const float Dt = 1f / 60f;

        public readonly PitchSpec Pitch;
        public readonly MatchSettings Settings;
        public readonly MatchClock Clock;
        public readonly MatchTeam[] Teams = new MatchTeam[2];
        public readonly List<MatchPlayer> Players = new List<MatchPlayer>();
        public readonly MatchEventLog Events = new MatchEventLog();
        public readonly Rng Rng;
        internal readonly MatchAi Ai;

        public BallState Ball;
        public Vec3 BallPrevPos;
        public MatchPlayer Owner { get; private set; }
        public MatchPlayer LastTouch { get; private set; }
        public MatchPlayer LastKicker { get; private set; }
        public KickKind LastKickKind { get; private set; }
        /// <summary>The player holding the ball in the hands (goalkeeper or throw-in taker).</summary>
        public MatchPlayer Holder { get; private set; }

        public MatchPhase Phase { get; private set; } = MatchPhase.PreMatch;
        public float PhaseTimer { get; private set; }
        public Restart CurrentRestart { get; private set; }
        public float SimTime { get; private set; }
        public int TrajectoryVersion { get; private set; }

        /// <summary>Team currently considered in possession (owner, holder or last touch).</summary>
        public int PossessionTeam { get; private set; } = -1;

        // User control
        public int UserTeam { get; private set; } = -1;
        public MatchPlayer UserPlayer { get; private set; }
        public PlayerCommand Command;
        /// <summary>When true the engine pauses at half time until ContinueFromHalfTime() is called.</summary>
        public bool HoldAtHalfTime;
        /// <summary>When true the AI takes over the user's player (e.g. simulating the rest of the match).</summary>
        public bool UserAutopilot;
        /// <summary>Lets the AI coach of the user's team substitute the user's athlete.</summary>
        public bool AllowCoachToSubUser = true;
        /// <summary>Training drills and tests can switch off AI decisions (players keep their targets).</summary>
        public bool AiEnabled = true;

        private float _accumulator;
        private int _kickOffTeamFirstHalf;
        private readonly List<(int team, MatchPlayer off, MatchPlayer on)> _pendingSubs = new List<(int, MatchPlayer, MatchPlayer)>();
        private int _nextId;

        public MatchEngine(TeamSheet home, TeamSheet away, MatchSettings settings, PitchSpec pitch = null)
        {
            Settings = (settings ?? new MatchSettings()).Clone();
            Settings.Validate();
            Pitch = pitch ?? new PitchSpec();
            Clock = new MatchClock(Settings.RealSecondsPerHalf, Settings.StoppageTimeScale);
            Rng = new Rng(Settings.Seed);
            Teams[0] = BuildTeam(0, home);
            Teams[1] = BuildTeam(1, away);
            Ai = new MatchAi(this);
            Ball.Position = new Vec3(0, BallPhysics.Radius, 0);
        }

        private MatchTeam BuildTeam(int index, TeamSheet sheet)
        {
            if (sheet.StartingXI == null || sheet.StartingXI.Count != 11)
                throw new ArgumentException($"Team '{sheet.Name}' must have exactly 11 starters (has {sheet.StartingXI?.Count ?? 0})");
            var team = new MatchTeam(index, sheet);
            for (int i = 0; i < 11; i++)
            {
                var mp = new MatchPlayer(_nextId++, sheet.StartingXI[i], index) { Starter = true };
                AssignSlot(team, mp, i);
                team.Players.Add(mp);
                Players.Add(mp);
            }
            if (sheet.Bench != null)
                foreach (var b in sheet.Bench)
                {
                    var mp = new MatchPlayer(_nextId++, b, index) { OnPitch = false };
                    team.Bench.Add(mp);
                }
            return team;
        }

        private static void AssignSlot(MatchTeam team, MatchPlayer mp, int slot)
        {
            var s = team.Formation.Slots[slot];
            mp.SlotIndex = slot;
            mp.Role = s.Role;
            mp.Position = s.Position;
        }

        // ------------------------------------------------------------------ queries

        /// <summary>+1 if the team attacks towards +X in the current half, otherwise -1.</summary>
        public int AttackDir(int team)
        {
            int d = team == 0 ? 1 : -1;
            if (Clock.Period == MatchPeriod.SecondHalf || Clock.Period == MatchPeriod.FullTime) d = -d;
            return d;
        }

        public Vec3 OpponentGoal(int team) => Pitch.GoalCentre(AttackDir(team));
        public Vec3 OwnGoal(int team) => Pitch.GoalCentre(-AttackDir(team));
        public int Score(int team) => Teams[team].Stats.Goals;
        public MatchPlayer FindPlayer(int id)
        {
            foreach (var p in Players) if (p.Id == id) return p;
            return null;
        }

        /// <summary>Finds a player on the pitch, substituted off or on the bench.</summary>
        public MatchPlayer FindPlayerAny(int id)
        {
            if (id < 0) return null;
            var p = FindPlayer(id);
            if (p != null) return p;
            foreach (var t in Teams)
            {
                foreach (var q in t.SubbedOff) if (q.Id == id) return q;
                foreach (var q in t.Bench) if (q.Id == id) return q;
            }
            return null;
        }

        /// <summary>X coordinate in the team's attacking frame (own goal = -HalfLength).</summary>
        public float AttackX(int team, Vec3 p) => p.X * AttackDir(team);

        public UserContext CurrentUserContext
        {
            get
            {
                if (UserPlayer == null || !UserPlayer.IsActive || UserAutopilot) return UserContext.None;
                if (Phase == MatchPhase.SetPiece && CurrentRestart.TakerId == UserPlayer.Id) return UserContext.SetPiece;
                if (Phase != MatchPhase.Playing) return UserContext.None;
                if (Owner == UserPlayer) return UserContext.Attacking;
                if (PossessionTeam == UserTeam) return UserContext.SupportingAttack;
                return UserContext.Defending;
            }
        }

        public bool IsFinished => Phase == MatchPhase.FullTime;

        // ------------------------------------------------------------------ setup

        /// <summary>Make the user control a team. In PlayerLock mode `lockedPlayerProfileId` selects the athlete.</summary>
        public void SetUserTeam(int team, string lockedPlayerProfileId = null)
        {
            UserTeam = team;
            if (UserPlayer != null) UserPlayer.UserControlled = false;
            UserPlayer = null;
            if (team < 0) return;
            if (Settings.ControlMode == ControlMode.PlayerLock && lockedPlayerProfileId != null)
            {
                foreach (var p in Teams[team].Players)
                    if (p.Profile.Id == lockedPlayerProfileId) UserPlayer = p;
                if (UserPlayer == null)
                    foreach (var p in Teams[team].Bench)
                        if (p.Profile.Id == lockedPlayerProfileId) { UserPlayer = p; break; }
            }
            if (UserPlayer == null)
            {
                foreach (var p in Teams[team].Players) if (p.Role == TacticalRole.Striker) { UserPlayer = p; break; }
                if (UserPlayer == null) UserPlayer = Teams[team].Players[Teams[team].Players.Count - 1];
            }
            UserPlayer.UserControlled = true;
        }

        public void StartMatch(int kickOffTeam = 0)
        {
            if (Phase != MatchPhase.PreMatch) return;
            _kickOffTeamFirstHalf = kickOffTeam;
            Clock.StartHalf(MatchPeriod.FirstHalf);
            BeginKickOff(kickOffTeam, true);
        }

        // ------------------------------------------------------------------ loop

        /// <summary>Advance by a variable real delta (frame time). Runs as many fixed steps as needed.</summary>
        public int Advance(float realDelta)
        {
            _accumulator += MathUtil.Clamp(realDelta, 0f, 0.25f) * Settings.GameSpeed;
            int steps = 0;
            while (_accumulator >= Dt && steps < 15)
            {
                Step();
                _accumulator -= Dt;
                steps++;
            }
            return steps;
        }

        /// <summary>Interpolation factor between the previous and current fixed step (for rendering).</summary>
        public float InterpolationAlpha => MathUtil.Clamp01(_accumulator / Dt);

        public void Step()
        {
            float dt = Dt;
            // Substitutions are applied between steps (never while iterating players) at a stoppage.
            if (_pendingSubs.Count > 0 && Phase != MatchPhase.Playing && Phase != MatchPhase.PreMatch) ApplyPendingSubstitutions();
            BallPrevPos = Ball.Position;
            foreach (var p in Players) p.PrevPos = p.Pos;
            SimTime += dt;

            switch (Phase)
            {
                case MatchPhase.PreMatch:
                case MatchPhase.FullTime:
                    return;
                case MatchPhase.HalfTime:
                    PhaseTimer -= dt;
                    if (PhaseTimer <= 0 && !HoldAtHalfTime) ContinueFromHalfTime();
                    return;
            }

            bool announcedBefore = Clock.StoppageAnnounced;
            Clock.Tick(dt);
            if (!announcedBefore && Clock.StoppageAnnounced)
                Emit(MatchEventType.StoppageAnnounced, -1, null, Vec3.Zero, Clock.StoppageMinutes);

            float vdt = Clock.ToVirtual(dt);
            foreach (var p in Players) if (p.IsActive) p.Stats.VirtualMinutesPlayed += vdt / 60f;

            if (Clock.HalfExpired && CanEndHalfNow())
            {
                EndHalf();
                return;
            }

            switch (Phase)
            {
                case MatchPhase.Playing: UpdatePlaying(dt); break;
                case MatchPhase.Stoppage: UpdateStoppage(dt); break;
                case MatchPhase.SetPiece: UpdateSetPiece(dt); break;
                case MatchPhase.GoalScored: UpdateGoalScored(dt); break;
            }
        }

        private bool CanEndHalfNow()
        {
            if (Phase == MatchPhase.GoalScored) return false;
            if (Phase == MatchPhase.SetPiece || Phase == MatchPhase.Stoppage)
                return CurrentRestart.Type != RestartType.Penalty;
            // In open play let a dangerous attack finish (ball in the final 25 m), max 20 extra virtual seconds.
            if (PossessionTeam >= 0 && AttackX(PossessionTeam, Ball.Position) > Pitch.HalfLength - 25f &&
                Clock.HalfVirtualSeconds < MatchClock.VirtualHalfSeconds + Clock.StoppageMinutes * 60f + 20f)
                return false;
            return true;
        }

        private void UpdatePlaying(float dt)
        {
            UpdatePossessionTeam();
            if (AiEnabled) Ai.Update(dt);
            if (UserPlayer != null && !UserAutopilot) ApplyUserCommand(dt);
            Command.ClearEdges();
            foreach (var p in Players) if (p.IsActive) UpdateAction(p, dt);
            foreach (var p in Players) if (p.IsActive) MovePlayer(p, dt);
            SeparatePlayers();
            UpdateBallControl(dt);
            bool hitWoodwork = BallPhysics.Step(ref Ball, dt, Pitch);
            if (hitWoodwork) Emit(MatchEventType.Woodwork, LastTouch?.Team ?? -1, LastTouch, Ball.Position, Ball.Velocity.Length);
            if (Holder != null) SyncHeldBall();
            CheckKeeperSaves();
            CheckLooseContacts();
            CheckRules();
            UpdateConditionAndStats(dt);
        }

        /// <summary>Advertising boards around the pitch stop balls that leave the field of play.</summary>
        private void ApplyBoards()
        {
            float bx = Pitch.HalfLength + 4.5f, bz = Pitch.HalfWidth + 4f;
            var p = Ball.Position; var v = Ball.Velocity;
            if (MathF.Abs(p.Z) > bz && p.Y < 1.2f) { p.Z = MathF.Sign(p.Z) * bz; v.Z = -v.Z * 0.3f; v.X *= 0.6f; }
            if (MathF.Abs(p.X) > bx && p.Y < 1.2f) { p.X = MathF.Sign(p.X) * bx; v.X = -v.X * 0.3f; v.Z *= 0.6f; }
            // Balls flying into the stands are returned by ball boys (stopped at the boards line).
            if (MathF.Abs(p.Z) > bz + 2f || MathF.Abs(p.X) > bx + 2f) { p = new Vec3(MathUtil.Clamp(p.X, -bx, bx), BallPhysics.Radius, MathUtil.Clamp(p.Z, -bz, bz)); v = Vec3.Zero; }
            Ball.Position = p; Ball.Velocity = v;
        }

        private void UpdatePossessionTeam()
        {
            if (Owner != null) PossessionTeam = Owner.Team;
            else if (Holder != null) PossessionTeam = Holder.Team;
            else if (LastTouch != null && (LastKickIsPass || Ball.Velocity.FlatLength < 3f)) PossessionTeam = LastTouch.Team;
        }

        private bool LastKickIsPass => _pendingPass;

        private void UpdateConditionAndStats(float dt)
        {
            float vdt = Clock.ToVirtual(dt);
            if (PossessionTeam >= 0) Teams[PossessionTeam].Stats.PossessionSeconds += vdt;
            foreach (var p in Players)
            {
                if (!p.IsActive) continue;
                float speed = p.Speed;
                // Distance is reported in "virtual" metres so it is comparable across match lengths.
                p.Stats.DistanceMeters += speed * dt * Clock.Rate * 0.5f;
                if (!Settings.FatigueEnabled) continue;
                float intensity = MathUtil.Clamp01(speed / MathF.Max(1f, p.MaxSpeed));
                float drain = (0.0034f + 0.014f * intensity * intensity) / 60f;
                float staminaFactor = MathUtil.Attr(p.A.Stamina, 1.45f, 0.65f);
                if (p.Profile.HasTrait(Traits.Engine)) staminaFactor *= 0.8f;
                if (p.IsGoalkeeper) staminaFactor *= 0.25f;
                float recover = intensity < 0.3f ? 0.0012f / 60f : 0f;
                p.Stamina = MathUtil.Clamp(p.Stamina - drain * staminaFactor * vdt + recover * vdt, 0.25f, 1f);
            }
        }

        // ------------------------------------------------------------------ phases

        private void BeginKickOff(int team, bool instantPlacement)
        {
            Owner = null; Holder = null; LastKicker = null; _pendingPass = false; ClearOffside();
            Ball = new BallState { Position = new Vec3(0, BallPhysics.Radius, 0) };
            var restart = new Restart { Type = RestartType.KickOff, Team = team, Spot = new Vec3(0, BallPhysics.Radius, 0) };
            restart.TakerId = Ai.ChooseTaker(restart).Id;
            CurrentRestart = restart;
            Ai.SetRestartTargets(restart);
            if (instantPlacement)
                foreach (var p in Players)
                    if (p.IsActive)
                    {
                        p.Pos = p.Target; p.PrevPos = p.Pos; p.Vel = Vec3.Zero;
                        p.Facing = AttackDir(p.Team) > 0 ? 0f : MathUtil.Pi;
                        p.ClearAction(); p.Stun = 0;
                    }
            Phase = MatchPhase.Stoppage;
            PhaseTimer = instantPlacement ? 1.0f : 2.0f;
        }

        public void BeginStoppage(Restart restart, float delay = 1.6f)
        {
            restart.Spot = new Vec3(restart.Spot.X, BallPhysics.Radius, restart.Spot.Z);
            var taker = Ai.ChooseTaker(restart);
            restart.TakerId = taker.Id;
            CurrentRestart = restart;
            Owner = null;
            if (Holder != null) { Holder = null; Ball.Held = false; }
            _pendingPass = false;
            ClearOffside();
            Phase = MatchPhase.Stoppage;
            PhaseTimer = delay;
            _stoppageElapsed = 0f;
            foreach (var p in Players) if (p.Action == PlayerAction.Tackle || p.Action == PlayerAction.Slide) { /* let it finish */ }
            Ai.SetRestartTargets(restart);
            Ai.CoachThink();
        }

        private float _stoppageElapsed;

        private void UpdateStoppage(float dt)
        {
            PhaseTimer -= dt;
            _stoppageElapsed += dt;
            // Ball keeps rolling briefly, then it is placed at the restart spot.
            if (_stoppageElapsed < 0.9f && CurrentRestart.Type != RestartType.KickOff) { BallPhysics.Step(ref Ball, dt, Pitch); ApplyBoards(); }
            else
            {
                Ball.Position = CurrentRestart.Spot;
                Ball.Velocity = Vec3.Zero;
                Ball.Spin = Vec3.Zero;
            }
            foreach (var p in Players) if (p.IsActive) { UpdateAction(p, dt); }
            Ai.UpdateRestartMovement(dt);
            foreach (var p in Players) if (p.IsActive) MovePlayer(p, dt);
            SeparatePlayers();

            var taker = FindPlayer(CurrentRestart.TakerId);
            if (taker == null || !taker.IsActive)
            {
                var r = CurrentRestart; r.TakerId = Ai.ChooseTaker(r).Id; CurrentRestart = r;
                return;
            }
            float takerDist = Vec3.FlatDistance(taker.Pos, Ai.TakerStandPoint(CurrentRestart));
            bool timeout = _stoppageElapsed > 7f;
            if (PhaseTimer <= 0 && (takerDist < 0.6f || timeout))
            {
                if (timeout && takerDist >= 0.6f)
                {
                    // Safety net: never stall a restart forever.
                    taker.Pos = Ai.TakerStandPoint(CurrentRestart);
                    taker.Vel = Vec3.Zero;
                }
                EnterSetPiece(taker);
            }
        }

        private void EnterSetPiece(MatchPlayer taker)
        {
            Phase = MatchPhase.SetPiece;
            PhaseTimer = 0;
            Ball.Position = CurrentRestart.Spot;
            Ball.Velocity = Vec3.Zero;
            taker.Vel = Vec3.Zero;
            taker.Facing = Ai.RestartFacing(CurrentRestart);
            if (CurrentRestart.Type == RestartType.ThrowIn)
            {
                Holder = taker;
                Ball.Held = true;
                SyncHeldBall();
            }
            else Owner = taker;
            if (Settings.ControlMode == ControlMode.TeamSwitching && taker.Team == UserTeam) SwitchUserTo(taker);
            Ai.OnSetPieceReady(CurrentRestart, taker);
        }

        private void UpdateSetPiece(float dt)
        {
            PhaseTimer += dt;
            var taker = FindPlayer(CurrentRestart.TakerId);
            if (taker == null) { Phase = MatchPhase.Playing; return; }

            bool userTaker = taker.UserControlled && !UserAutopilot;
            if (userTaker && PhaseTimer > 0.4f && taker.Action == PlayerAction.None)
            {
                ApplyUserSetPiece(taker);
            }
            if (taker.Action == PlayerAction.None && (!userTaker || PhaseTimer > 12f))
                Ai.TakeSetPiece(CurrentRestart, taker, PhaseTimer);
            Command.ClearEdges();

            foreach (var p in Players) if (p.IsActive) UpdateAction(p, dt);
            Ai.UpdateRestartMovement(dt);
            foreach (var p in Players) if (p.IsActive) MovePlayer(p, dt);
            SeparatePlayers();

            if (Holder == taker) SyncHeldBall();
            else if (Owner == taker && !taker.ContactDone)
            {
                // Hold the ball on the spot until the kick contact.
                Ball.Position = CurrentRestart.Spot;
                Ball.Velocity = Vec3.Zero;
            }

            if (taker.ContactDone || (Owner != taker && Holder != taker))
            {
                Phase = MatchPhase.Playing;
                _restartExemptOffside = CurrentRestart.Type == RestartType.ThrowIn ||
                                        CurrentRestart.Type == RestartType.Corner ||
                                        CurrentRestart.Type == RestartType.GoalKick;
                if (_restartExemptOffside) ClearOffside();
            }
        }

        private bool _restartExemptOffside;

        private float _goalCelebrationTimer;
        private int _concedingTeam;

        private void UpdateGoalScored(float dt)
        {
            _goalCelebrationTimer -= dt;
            BallPhysics.Step(ref Ball, dt, Pitch);
            ApplyBoards();
            foreach (var p in Players) if (p.IsActive) UpdateAction(p, dt);
            Ai.UpdateCelebration(dt);
            foreach (var p in Players) if (p.IsActive) MovePlayer(p, dt);
            SeparatePlayers();
            if (_goalCelebrationTimer <= 0)
            {
                foreach (var p in Players) if (p.Action == PlayerAction.Celebrate || p.Action == PlayerAction.Dejected) p.ClearAction();
                BeginKickOff(_concedingTeam, false);
            }
        }

        /// <summary>Skip the current celebration / cutscene-like pause (UI "skip" button).</summary>
        public void SkipCelebration()
        {
            if (Phase == MatchPhase.GoalScored) _goalCelebrationTimer = MathF.Min(_goalCelebrationTimer, 0.01f);
            if (Phase == MatchPhase.HalfTime) PhaseTimer = 0;
        }

        private void EndHalf()
        {
            Owner = null; Holder = null; Ball.Held = false;
            if (Clock.Period == MatchPeriod.FirstHalf)
            {
                Clock.EnterHalfTime();
                Phase = MatchPhase.HalfTime;
                PhaseTimer = 3f;
                Emit(MatchEventType.HalfTime, -1, null, Ball.Position, 0);
            }
            else
            {
                Clock.EnterFullTime();
                Phase = MatchPhase.FullTime;
                Emit(MatchEventType.FullTime, -1, null, Ball.Position, 0);
                FinalizeRatings();
            }
        }

        public void ContinueFromHalfTime()
        {
            if (Phase != MatchPhase.HalfTime) return;
            Clock.StartHalf(MatchPeriod.SecondHalf);
            foreach (var p in Players) p.Stamina = MathUtil.Clamp(p.Stamina + 0.18f, 0f, 1f);
            ApplyPendingSubstitutions();
            Emit(MatchEventType.SecondHalf, -1, null, Vec3.Zero, 0);
            BeginKickOff(1 - _kickOffTeamFirstHalf, true);
        }

        // ------------------------------------------------------------------ goals & rules

        private void CheckRules()
        {
            if (Holder != null) return;
            float r = BallPhysics.Radius;
            for (int side = -1; side <= 1; side += 2)
            {
                float line = Pitch.HalfLength + r;
                bool crossed = BallPrevPos.X * side <= line && Ball.Position.X * side > line;
                if (!crossed) continue;
                // Interpolate the crossing point so fast balls are judged where they crossed.
                float t = MathUtil.InverseLerp(BallPrevPos.X * side, Ball.Position.X * side, line);
                Vec3 cross = Vec3.Lerp(BallPrevPos, Ball.Position, t);
                bool inMouth = MathF.Abs(cross.Z) < Pitch.HalfGoalWidth - r * 0.2f && cross.Y < Pitch.GoalHeight - r * 0.2f;
                int attacking = AttackDir(0) == side ? 0 : 1;
                if (inMouth) { OnGoal(attacking); return; }

                int defending = 1 - attacking;
                if (LastTouch != null && LastTouch.Team == defending)
                {
                    float cz = MathF.Sign(cross.Z == 0 ? 1 : cross.Z) * (Pitch.HalfWidth - 0.3f);
                    var spot = new Vec3(side * (Pitch.HalfLength - 0.3f), r, cz);
                    Teams[attacking].Stats.Corners++;
                    Emit(MatchEventType.Corner, attacking, null, spot, 0);
                    BeginStoppage(new Restart { Type = RestartType.Corner, Team = attacking, Spot = spot });
                }
                else
                {
                    var spot = new Vec3(side * (Pitch.HalfLength - Pitch.GoalAreaDepth), r, MathF.Sign(cross.Z == 0 ? 1 : cross.Z) * 4.5f);
                    Emit(MatchEventType.GoalKick, defending, null, spot, 0);
                    if (_activeShot && LastTouch != null && LastTouch.Team == attacking) Emit(MatchEventType.ChanceMissed, attacking, LastTouch, cross, _activeShotXg);
                    BeginStoppage(new Restart { Type = RestartType.GoalKick, Team = defending, Spot = spot });
                }
                _activeShot = false;
                return;
            }

            if (MathF.Abs(Ball.Position.Z) > Pitch.HalfWidth + r)
            {
                int team = LastTouch != null ? 1 - LastTouch.Team : (PossessionTeam >= 0 ? 1 - PossessionTeam : 0);
                var spot = new Vec3(MathUtil.Clamp(Ball.Position.X, -Pitch.HalfLength + 1, Pitch.HalfLength - 1), r, MathF.Sign(Ball.Position.Z) * Pitch.HalfWidth);
                Emit(MatchEventType.ThrowIn, team, null, spot, 0);
                BeginStoppage(new Restart { Type = RestartType.ThrowIn, Team = team, Spot = spot });
            }
        }

        private void OnGoal(int scoringTeam)
        {
            var team = Teams[scoringTeam];
            team.Stats.Goals++;
            MatchPlayer scorer = LastTouch;
            bool ownGoal = scorer != null && scorer.Team != scoringTeam;
            MatchPlayer assist = null;
            if (!ownGoal && scorer != null)
            {
                scorer.Stats.Goals++;
                if (_lastCompletedPassFrom != null && _lastCompletedPassTo == scorer && _lastCompletedPassFrom.Team == scoringTeam && SimTime - _lastCompletedPassTime < 12f)
                {
                    assist = _lastCompletedPassFrom;
                    assist.Stats.Assists++;
                }
            }
            var gk = Teams[1 - scoringTeam].Goalkeeper;
            if (gk != null) gk.Stats.GoalsConceded++;
            foreach (var p in Teams[1 - scoringTeam].Players)
                if (p.IsActive && p.Role != TacticalRole.Goalkeeper && (p.Role == TacticalRole.CentreBack || p.Role == TacticalRole.FullBack))
                    p.Stats.GoalsConceded++;

            Emit(MatchEventType.Goal, scoringTeam, scorer, Ball.Position, ownGoal ? 1 : 0, assist);
            Clock.AddStoppage(35f);
            _activeShot = false;
            Owner = null;
            Phase = MatchPhase.GoalScored;
            _goalCelebrationTimer = 4.5f;
            _concedingTeam = 1 - scoringTeam;
            Ai.StartCelebration(scoringTeam, ownGoal ? null : scorer);
        }

        internal void CallFoul(MatchPlayer offender, MatchPlayer victim, Vec3 where, bool reckless)
        {
            if (!Settings.FoulsEnabled) return;
            offender.Stats.Fouls++;
            Teams[offender.Team].Stats.Fouls++;
            if (victim != null) victim.Stats.FoulsSuffered++;
            Emit(MatchEventType.Foul, offender.Team, offender, where, reckless ? 1 : 0, victim);
            Clock.AddStoppage(12f);

            float yellowChance = reckless ? 0.45f : 0.08f + offender.A.Aggression / 900f;
            if (Rng.Chance(yellowChance))
            {
                offender.Yellow++;
                offender.Stats.YellowCards++;
                Teams[offender.Team].Stats.YellowCards++;
                Emit(MatchEventType.YellowCard, offender.Team, offender, where, offender.Yellow);
                if (offender.Yellow >= 2) SendOff(offender);
            }

            if (Settings.InjuriesEnabled && victim != null && Rng.Chance(reckless ? 0.06f : 0.012f))
            {
                victim.Injured = true;
                Emit(MatchEventType.Injury, victim.Team, victim, where, 0);
                Clock.AddStoppage(45f);
            }

            int fouledTeam = 1 - offender.Team;
            int defendedSide = -AttackDir(offender.Team);
            if (Pitch.IsInsidePenaltyArea(where, defendedSide))
            {
                Emit(MatchEventType.Penalty, fouledTeam, victim, where, 0);
                BeginStoppage(new Restart { Type = RestartType.Penalty, Team = fouledTeam, Spot = Pitch.PenaltySpot(defendedSide) }, 2.5f);
            }
            else
            {
                var spot = Pitch.ClampToField(where, 0.5f);
                Emit(MatchEventType.FreeKick, fouledTeam, victim, spot, 0);
                BeginStoppage(new Restart { Type = RestartType.FreeKick, Team = fouledTeam, Spot = spot }, 2.0f);
            }
        }

        private void SendOff(MatchPlayer p)
        {
            p.SentOff = true;
            p.Stats.RedCards++;
            Teams[p.Team].Stats.RedCards++;
            Emit(MatchEventType.RedCard, p.Team, p, p.Pos, 0);
            if (Owner == p) Owner = null;
            if (p.UserControlled && Settings.ControlMode == ControlMode.TeamSwitching) SwitchUserToBest();
        }

        // ------------------------------------------------------------------ substitutions

        /// <summary>Queue a substitution; it is applied at the next stoppage.</summary>
        public bool RequestSubstitution(int team, int offPlayerId, int benchIndex)
        {
            var t = Teams[team];
            if (t.SubstitutionsUsed + _pendingSubs.FindAll(s => s.team == team).Count >= Settings.MaxSubstitutions) return false;
            if (benchIndex < 0 || benchIndex >= t.Bench.Count) return false;
            var off = t.Players.Find(p => p.Id == offPlayerId && p.IsActive);
            var on = t.Bench[benchIndex];
            if (off == null || on == null) return false;
            if (_pendingSubs.Exists(s => s.off == off || s.on == on)) return false;
            _pendingSubs.Add((team, off, on));
            return true;
        }

        private void ApplyPendingSubstitutions()
        {
            if (_pendingSubs.Count == 0) return;
            foreach (var (team, off, on) in _pendingSubs)
            {
                var t = Teams[team];
                if (!off.IsActive || !t.Bench.Contains(on)) continue;
                int idx = t.Players.IndexOf(off);
                if (idx < 0) continue;
                off.OnPitch = false;
                t.SubbedOff.Add(off);
                t.Bench.Remove(on);
                on.OnPitch = true;
                AssignSlot(t, on, off.SlotIndex);
                on.Pos = new Vec3(0, 0, -Pitch.HalfWidth - 0.5f);
                on.PrevPos = on.Pos;
                on.Facing = MathUtil.Pi * 0.5f;
                on.Team = team;
                t.Players[idx] = on;
                int pi = Players.IndexOf(off);
                if (pi >= 0) Players[pi] = on; else Players.Add(on);
                t.SubstitutionsUsed++;
                t.Stats.Substitutions++;
                if (Owner == off) Owner = null;
                if (off == UserPlayer)
                {
                    off.UserControlled = false;
                    if (Settings.ControlMode == ControlMode.TeamSwitching) { UserPlayer = on; on.UserControlled = true; }
                    else UserSubbedOff = true;
                }
                if (on.Profile.Id != null && UserPlayer != null && UserPlayer.Profile.Id == on.Profile.Id)
                {
                    UserPlayer = on; on.UserControlled = true;
                }
                Clock.AddStoppage(25f);
                Emit(MatchEventType.Substitution, team, on, on.Pos, 0, off);
            }
            _pendingSubs.Clear();
        }

        /// <summary>True when the user's locked athlete has been substituted off.</summary>
        public bool UserSubbedOff { get; private set; }

        // ------------------------------------------------------------------ events & ratings

        internal void Emit(MatchEventType type, int team, MatchPlayer p, Vec3 pos, float value, MatchPlayer other = null)
        {
            Events.Add(new MatchEvent
            {
                Type = type,
                VirtualTime = Clock.TotalVirtualSeconds,
                Minute = Math.Max(1, (int)(Clock.TotalVirtualSeconds / 60f) + 1),
                Team = team,
                PlayerId = p?.Id ?? -1,
                OtherPlayerId = other?.Id ?? -1,
                Position = pos,
                Value = value
            });
        }

        public void FinalizeRatings()
        {
            for (int t = 0; t < 2; t++)
            {
                int diff = Score(t) - Score(1 - t);
                var all = new List<MatchPlayer>(Teams[t].Players);
                all.AddRange(Teams[t].SubbedOff);
                foreach (var p in all)
                {
                    var s = p.Stats;
                    if (s.VirtualMinutesPlayed < 1f) continue;
                    float r = 6.0f;
                    r += s.Goals * 1.0f + s.Assists * 0.65f + s.ShotsOnTarget * 0.12f + s.KeyPasses * 0.15f;
                    if (s.Passes >= 5) r += MathUtil.Clamp((s.PassAccuracy - 0.75f) * 2.5f, -0.6f, 0.5f);
                    r += s.TacklesWon * 0.12f + s.Interceptions * 0.1f + s.Clearances * 0.05f + s.DribblesWon * 0.08f;
                    if (p.IsGoalkeeper) r += s.Saves * 0.3f - s.GoalsConceded * 0.35f + (s.GoalsConceded == 0 ? 0.6f : 0f);
                    else if (p.Role == TacticalRole.CentreBack || p.Role == TacticalRole.FullBack) r -= s.GoalsConceded * 0.15f;
                    r -= s.Fouls * 0.08f + s.YellowCards * 0.3f + s.RedCards * 1.5f;
                    r += MathUtil.Clamp(diff, -3, 3) * 0.15f;
                    float exposure = MathUtil.Clamp01(s.VirtualMinutesPlayed / 90f);
                    r = 6.0f + (r - 6.0f) * (0.6f + 0.4f * exposure);
                    s.Rating = MathUtil.Clamp(r, 3f, 10f);
                }
            }
        }

        /// <summary>
        /// Puts the match straight into open play with the given ball state (training drills, tests,
        /// scripted scenarios). Clears ownership, restarts and offside state.
        /// </summary>
        public void ForceOpenPlay(Vec3 ballPos, Vec3 ballVel, MatchPlayer lastTouch = null)
        {
            if (!Clock.IsRunning) Clock.StartHalf(MatchPeriod.FirstHalf);
            Phase = MatchPhase.Playing;
            Ball = new BallState { Position = ballPos, Velocity = ballVel };
            BallPrevPos = ballPos;
            Owner = null; Holder = null;
            LastTouch = lastTouch; LastKicker = lastTouch;
            _pendingPass = false; _activeShot = false;
            ClearOffside();
            foreach (var p in Players) { p.ClearAction(); p.Stun = 0f; p.TouchCooldown = 0f; }
        }

        /// <summary>Gives the ball to a player at their feet (drills, tests).</summary>
        public void GiveBallTo(MatchPlayer p)
        {
            Owner = p; Holder = null; Ball.Held = false;
            Vec3 f = p.FootPoint();
            Ball.Position = new Vec3(f.X, BallPhysics.Radius, f.Z);
            Ball.Velocity = p.Vel;
            LastTouch = p;
            p.NextTouchTime = SimTime + 0.2f;
            PossessionTeam = p.Team;
        }

        /// <summary>Teleports a player (drills, tests, formation setup).</summary>
        public void PlacePlayer(MatchPlayer p, Vec3 pos, float facing)
        {
            p.Pos = pos; p.PrevPos = pos; p.Vel = Vec3.Zero; p.Facing = facing; p.Target = pos;
        }

        /// <summary>Runs the whole match headless as fast as possible (used by sims and tests).</summary>
        public void RunToEnd(int maxSteps = 2_000_000)
        {
            if (Phase == MatchPhase.PreMatch) StartMatch();
            int n = 0;
            while (Phase != MatchPhase.FullTime && n++ < maxSteps)
            {
                if (Phase == MatchPhase.HalfTime) ContinueFromHalfTime();
                Step();
            }
        }
    }
}
