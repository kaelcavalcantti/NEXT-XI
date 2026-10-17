using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core.Data;
using NextXI.Core.Match;

namespace NextXI.Core.Career
{
    /// <summary>
    /// Playable training exercises built on the real match engine (same physics, AI and
    /// controls as matches). Each drill runs a series of attempts with objectives and produces a
    /// 0..1 score that feeds career progression. Engine-independent: runs headless in tests.
    /// </summary>
    public sealed class DrillRun
    {
        public readonly string DrillId;
        public readonly MatchEngine Engine;
        public readonly MatchPlayer User;
        public int Attempt { get; private set; }
        public int Attempts { get; private set; }
        public int Successes { get; private set; }
        public bool Finished { get; private set; }
        public string Status { get; private set; } = "";
        public string Instructions { get; private set; } = "";
        public float AttemptTime { get; private set; }
        public float AttemptLimit { get; private set; } = 7f;
        /// <summary>Waypoints/targets to render (gates, zones).</summary>
        public readonly List<Vec3> Targets = new List<Vec3>();
        public int TargetIndex { get; private set; }
        /// <summary>True when the result is computed from attributes (keeper drill without user control).</summary>
        public bool Simulated { get; private set; }

        private readonly Rng _rng;
        private readonly List<MatchEvent> _events = new List<MatchEvent>();
        private readonly List<MatchPlayer> _participants = new List<MatchPlayer>();
        private float _bestTimeScoreSum;
        private float _pause;
        private float _holdTime;
        private int _dir;

        public float Score
        {
            get
            {
                if (Attempts == 0) return 0;
                if (DrillId == "speed" || DrillId == "agility") return MathUtil.Clamp01(_bestTimeScoreSum / Attempts);
                return MathUtil.Clamp01(Successes / (float)Attempts);
            }
        }

        public DrillRun(string drillId, PlayerProfile athlete, TeamSheet own, TeamSheet sparring, ulong seed, Difficulty difficulty = Difficulty.Professional)
        {
            DrillId = drillId;
            _rng = new Rng(seed);
            // Make sure the athlete is part of the own sheet's XI.
            if (!own.StartingXI.Any(p => p.Id == athlete.Id))
            {
                int slot = own.StartingXI.FindIndex(p => p.Position != Position.GK);
                if (athlete.Position == Position.GK) slot = 0;
                else
                {
                    int best = slot; float fit = float.MinValue;
                    var f = Formation.Get(own.FormationName);
                    for (int i = 1; i < f.Slots.Length; i++)
                    {
                        float ft = TeamSheetBuilder.Fit(athlete, f.Slots[i].Position);
                        if (ft > fit) { fit = ft; best = i; }
                    }
                    slot = best;
                }
                own.StartingXI[slot] = athlete;
            }
            var settings = new MatchSettings { RealMinutesPerHalf = 10, Seed = seed, OffsideEnabled = false, ControlMode = ControlMode.PlayerLock, Difficulty = difficulty, FatigueEnabled = false, InjuriesEnabled = false };
            Engine = new MatchEngine(own, sparring, settings);
            Engine.SetUserTeam(0, athlete.Id);
            Engine.AllowCoachToSubUser = false;
            Engine.StartMatch();
            User = Engine.UserPlayer;
            _dir = Engine.AttackDir(0);
            Configure();
            NextAttempt();
        }

        private MatchPlayer Mate(int i) => Engine.Teams[0].Players.Where(p => p != User && !p.IsGoalkeeper).ElementAt(i);
        private MatchPlayer Opp(int i) => Engine.Teams[1].Players.Where(p => !p.IsGoalkeeper).ElementAt(i);
        private MatchPlayer OppKeeper => Engine.Teams[1].Players.First(p => p.IsGoalkeeper);
        private MatchPlayer OwnKeeper => Engine.Teams[0].Players.First(p => p.IsGoalkeeper);

        private void Configure()
        {
            _participants.Clear();
            _participants.Add(User);
            switch (DrillId)
            {
                case "finishing": Attempts = 8; AttemptLimit = 6f; _participants.Add(OppKeeper); _participants.Add(Opp(1)); Instructions = "Finalize contra o goleiro. Um zagueiro pressiona."; break;
                case "heading": Attempts = 6; AttemptLimit = 5f; _participants.Add(OppKeeper); _participants.Add(Mate(0)); Instructions = "Ataque o cruzamento e cabeceie (CHUTE ao chegar a bola)."; break;
                case "passing": Attempts = 10; AttemptLimit = 6f; for (int i = 0; i < 3; i++) _participants.Add(Mate(i)); _participants.Add(Opp(0)); _participants.Add(Opp(1)); Instructions = "Complete passes para os companheiros. Dois marcadores tentam interceptar."; break;
                case "control": Attempts = 8; AttemptLimit = 5f; _participants.Add(Mate(0)); _participants.Add(Opp(0)); Instructions = "Domine o lançamento com um primeiro toque limpo."; break;
                case "positioning": Attempts = 8; AttemptLimit = 8f; _participants.Add(Mate(0)); _participants.Add(Opp(0)); _participants.Add(Opp(1)); Instructions = "Desmarque-se e peça o passe (PASSE sem bola)."; break;
                case "dribbling": Attempts = 6; AttemptLimit = 8f; _participants.Add(Opp(0)); _participants.Add(Opp(1)); Instructions = "Leve a bola até a grande área passando pelos marcadores."; break;
                case "defending": Attempts = 6; AttemptLimit = 8f; _participants.Add(Opp(7)); _participants.Add(OwnKeeper); Instructions = "Desarme o atacante sem cometer falta."; break;
                case "speed": Attempts = 3; AttemptLimit = 8f; Instructions = "Arranque e cruze o portão de chegada (segure CORRER)."; break;
                case "agility": Attempts = 3; AttemptLimit = 12f; Instructions = "Passe pelos cones na ordem, o mais rápido possível."; break;
                case "goalkeeping": Attempts = 10; AttemptLimit = 4f; Simulated = User.IsGoalkeeper; _participants.Add(Opp(8)); Instructions = "Sequência de chutes: posicione-se e defenda."; break;
                default: Attempts = 5; Instructions = ""; break;
            }
            foreach (var p in Engine.Players) p.OnPitch = _participants.Contains(p);
        }

        private Vec3 Spot(float axFromGoal, float z) => new Vec3(_dir * (Engine.Pitch.HalfLength - axFromGoal), 0, z);

        private void Place(MatchPlayer p, Vec3 pos, bool anchor)
        {
            Engine.PlacePlayer(p, pos, (Engine.OpponentGoal(p.Team) - pos).FlatNormalized.FlatAngle);
            p.Anchor = anchor ? pos : (Vec3?)null;
        }

        private void NextAttempt()
        {
            if (Attempt >= Attempts) { Finished = true; Status = $"Fim do treino: {Score * 100:0} pontos"; return; }
            Attempt++;
            AttemptTime = 0f;
            _holdTime = 0f;
            Targets.Clear();
            TargetIndex = 0;
            switch (DrillId)
            {
                case "finishing":
                {
                    var pos = Spot(_rng.Range(13f, 24f), _rng.Range(-13f, 13f));
                    Place(User, pos, false);
                    Place(OppKeeper, Spot(0.8f, 0), false);
                    Place(Opp(1), Spot(_rng.Range(6f, 9f), pos.Z * 0.5f), false);
                    Engine.ForceOpenPlay(pos, Vec3.Zero, User);
                    Engine.GiveBallTo(User);
                    break;
                }
                case "heading":
                {
                    var mate = Mate(0);
                    float zs = _rng.Chance(0.5f) ? 1f : -1f;
                    var crossPos = Spot(14f, zs * 26f);
                    Place(mate, crossPos, true);
                    Place(User, Spot(_rng.Range(9f, 13f), -zs * _rng.Range(0f, 5f)), false);
                    Place(OppKeeper, Spot(0.8f, 0), false);
                    Engine.ForceOpenPlay(crossPos, Vec3.Zero, mate);
                    Engine.GiveBallTo(mate);
                    var target = Spot(_rng.Range(9.5f, 12.5f), _rng.Range(-3f, 3f));
                    Engine.StartKick(mate, new PendingKick { Kind = KickKind.Cross, Target = target, ReceiverId = User.Id, Power = 0.6f, ErrorScale = 0.6f });
                    Targets.Add(target);
                    break;
                }
                case "passing":
                {
                    var center = Spot(45f, 0);
                    Place(User, center, false);
                    for (int i = 0; i < 3; i++)
                    {
                        float ang = (i - 1) * 1.1f + _rng.Range(-0.3f, 0.3f);
                        var at = center + new Vec3(_dir * MathF.Cos(ang), 0, MathF.Sin(ang)) * _rng.Range(12f, 24f);
                        Place(Mate(i), Engine.Pitch.ClampToField(at, 3f), true);
                        Targets.Add(Mate(i).Pos);
                    }
                    Place(Opp(0), center + new Vec3(_dir * 6f, 0, 4f), false);
                    Place(Opp(1), center + new Vec3(_dir * 9f, 0, -5f), false);
                    Engine.ForceOpenPlay(center, Vec3.Zero, User);
                    Engine.GiveBallTo(User);
                    break;
                }
                case "control":
                {
                    var mate = Mate(0);
                    var from = Spot(60f, _rng.Range(-15f, 15f));
                    var to = Spot(35f, _rng.Range(-12f, 12f));
                    Place(mate, from, true);
                    Place(User, to + new Vec3(_rng.Range(-3f, 3f), 0, _rng.Range(-3f, 3f)), false);
                    Place(Opp(0), to + new Vec3(_dir * 5f, 0, 3f), false);
                    Engine.ForceOpenPlay(from, Vec3.Zero, mate);
                    Engine.GiveBallTo(mate);
                    Engine.StartKick(mate, new PendingKick { Kind = KickKind.Lofted, Target = to, ReceiverId = User.Id, Power = 0.6f, ErrorScale = 0.8f });
                    Targets.Add(to);
                    break;
                }
                case "positioning":
                {
                    var mate = Mate(0);
                    var from = Spot(50f, 0);
                    Place(mate, from, true);
                    Place(User, Spot(30f, _rng.Range(-10f, 10f)), false);
                    Place(Opp(0), User.Pos + new Vec3(-_dir * 1.5f, 0, 0), false);
                    Place(Opp(1), Spot(38f, -User.Pos.Z * 0.5f), false);
                    Engine.ForceOpenPlay(from, Vec3.Zero, mate);
                    Engine.GiveBallTo(mate);
                    break;
                }
                case "dribbling":
                {
                    var start = Spot(42f, _rng.Range(-10f, 10f));
                    Place(User, start, false);
                    Place(Opp(0), start + new Vec3(_dir * 8f, 0, 2f), false);
                    Place(Opp(1), start + new Vec3(_dir * 15f, 0, -3f), false);
                    Engine.ForceOpenPlay(start, Vec3.Zero, User);
                    Engine.GiveBallTo(User);
                    Targets.Add(Spot(16f, 0));
                    break;
                }
                case "defending":
                {
                    var att = Opp(7);
                    var start = new Vec3(-_dir * (Engine.Pitch.HalfLength - 38f), 0, _rng.Range(-10f, 10f));
                    Place(att, start, false);
                    Place(User, start + new Vec3(-_dir * 9f, 0, _rng.Range(-2f, 2f)), false);
                    Place(OwnKeeper, new Vec3(-_dir * (Engine.Pitch.HalfLength - 1f), 0, 0), false);
                    Engine.ForceOpenPlay(start, Vec3.Zero, att);
                    Engine.GiveBallTo(att);
                    break;
                }
                case "speed":
                {
                    var start = Spot(60f, 0);
                    Place(User, start, false);
                    Engine.ForceOpenPlay(start + new Vec3(0, 0, 20f), Vec3.Zero, null);
                    Targets.Add(start + new Vec3(_dir * 30f, 0, 0));
                    break;
                }
                case "agility":
                {
                    var start = Spot(60f, -8f);
                    Place(User, start, false);
                    Engine.ForceOpenPlay(start + new Vec3(0, 0, 25f), Vec3.Zero, null);
                    for (int i = 1; i <= 5; i++) Targets.Add(start + new Vec3(_dir * 5f * i, 0, (i % 2 == 0 ? -1f : 1f) * 4f));
                    break;
                }
                case "goalkeeping":
                {
                    var shooter = Opp(8);
                    var gk = User.IsGoalkeeper ? User : OwnKeeper;
                    var pos = new Vec3(-_dir * (Engine.Pitch.HalfLength - _rng.Range(12f, 22f)), 0, _rng.Range(-12f, 12f));
                    Place(shooter, pos, false);
                    Place(gk, new Vec3(-_dir * (Engine.Pitch.HalfLength - 1f), 0, 0), false);
                    Engine.ForceOpenPlay(pos, Vec3.Zero, shooter);
                    Engine.GiveBallTo(shooter);
                    var target = new Vec3(-_dir * (Engine.Pitch.HalfLength + 0.3f), _rng.Range(0.3f, 2f), _rng.Range(-3.2f, 3.2f));
                    Engine.StartKick(shooter, new PendingKick { Kind = KickKind.Shot, IsShot = true, Target = target, Power = _rng.Range(0.55f, 0.9f), ErrorScale = 0.6f });
                    break;
                }
            }
            Engine.Events.Drain(_events);
            _events.Clear();
            Status = $"Tentativa {Attempt}/{Attempts}";
        }

        /// <summary>Advance one fixed step (call at 60 Hz).</summary>
        public void Step()
        {
            if (Finished) return;
            if (_pause > 0f)
            {
                _pause -= MatchEngine.Dt;
                if (_pause <= 0f) NextAttempt();
                return;
            }
            // Drills ignore restarts: a stoppage ends the attempt instead.
            Engine.Step();
            AttemptTime += MatchEngine.Dt;
            _events.Clear();
            Engine.Events.Drain(_events);
            bool? result = Evaluate();
            if (result.HasValue) EndAttempt(result.Value);
            else if (AttemptTime >= AttemptLimit) EndAttempt(false, "Tempo esgotado");
        }

        private bool Has(MatchEventType t, int team = -1, int player = -1) =>
            _events.Any(e => e.Type == t && (team < 0 || e.Team == team) && (player < 0 || e.PlayerId == player));

        private bool StoppedPlay => Engine.Phase != MatchPhase.Playing;

        private bool? Evaluate()
        {
            switch (DrillId)
            {
                case "finishing":
                case "heading":
                    if (Has(MatchEventType.Goal, 0)) return true;
                    if (Has(MatchEventType.Save) || Has(MatchEventType.KeeperClaim) || StoppedPlay) return false;
                    if (Engine.Owner != null && Engine.Owner.Team == 1) return false;
                    return null;
                case "passing":
                    if (_events.Any(e => e.Type == MatchEventType.PassCompleted && e.PlayerId == User.Id)) return true;
                    if (Has(MatchEventType.Interception, 1) || StoppedPlay || Engine.Owner != null && Engine.Owner.Team == 1) return false;
                    return null;
                case "control":
                    // A clean first touch that keeps the ball under control (or a pass completed after it).
                    if (Engine.Owner == User) { _holdTime += MatchEngine.Dt; if (_holdTime >= 0.6f) return true; }
                    else if (_holdTime > 0.05f && !_events.Any(e => e.PlayerId == User.Id && (e.Type == MatchEventType.Pass || e.Type == MatchEventType.Shot))) return false;
                    else if (_holdTime > 0.05f) return true;
                    if (StoppedPlay || Engine.Owner != null && Engine.Owner.Team == 1) return false;
                    return null;
                case "positioning":
                    if (Engine.Owner == User) return true;
                    if (StoppedPlay || Engine.Owner != null && Engine.Owner.Team == 1 || Has(MatchEventType.Interception, 1)) return false;
                    return null;
                case "dribbling":
                    if (Engine.Owner == User && Engine.Pitch.IsInsidePenaltyArea(User.Pos, _dir) || Has(MatchEventType.Goal, 0)) return true;
                    if (StoppedPlay || Engine.Owner != null && Engine.Owner.Team == 1) return false;
                    return null;
                case "defending":
                    if (Has(MatchEventType.Foul, 0)) return false;
                    // Only the athlete's own ball-winning counts.
                    if (Engine.Owner == User || Has(MatchEventType.TackleWon, 0, User.Id) || Has(MatchEventType.Interception, 0, User.Id)) return true;
                    if (Engine.Owner != null && Engine.Owner.Team == 0 || Engine.Holder != null) return false;
                    if (Has(MatchEventType.Goal) || Has(MatchEventType.Shot, 1) || StoppedPlay) return false;
                    return null;
                case "speed":
                case "agility":
                {
                    var t = Targets[TargetIndex];
                    if (Vec3.FlatDistance(User.Pos, t) < (DrillId == "speed" ? 2.0f : 1.6f))
                    {
                        TargetIndex++;
                        if (TargetIndex >= Targets.Count)
                        {
                            // Time score: 30 m in ~3.6 s is elite, ~5.5 s slow (agility scaled).
                            float best = DrillId == "speed" ? 3.7f : 6.0f, slow = DrillId == "speed" ? 5.8f : 10.5f;
                            _bestTimeScoreSum += MathUtil.Clamp01((slow - AttemptTime) / (slow - best));
                            return true;
                        }
                    }
                    return null;
                }
                case "goalkeeping":
                    if (Has(MatchEventType.Save, 0) || Has(MatchEventType.KeeperClaim, 0)) return true;
                    if (Has(MatchEventType.Goal) || StoppedPlay && AttemptTime > 0.5f) return Has(MatchEventType.Goal) ? false : (bool?)true;
                    return null;
            }
            return null;
        }

        private void EndAttempt(bool success, string why = null)
        {
            if (success && DrillId != "speed" && DrillId != "agility") Successes++;
            if (!success && (DrillId == "speed" || DrillId == "agility")) { /* no time score */ }
            Status = success ? "Boa!" : (why ?? "Não foi dessa vez");
            _pause = 1.2f;
        }

        /// <summary>Headless helper: runs the whole drill with the AI controlling the athlete.</summary>
        public float RunHeadless(int maxSteps = 100000)
        {
            Engine.UserAutopilot = true;
            int n = 0;
            while (!Finished && n++ < maxSteps)
            {
                // Autopilot for movement drills: run straight at the next target.
                if ((DrillId == "speed" || DrillId == "agility") && TargetIndex < Targets.Count)
                {
                    User.Target = Targets[TargetIndex];
                    User.TargetSpeedFactor = 1f;
                    Engine.AiEnabled = false;
                }
                Step();
            }
            return Score;
        }
    }
}
