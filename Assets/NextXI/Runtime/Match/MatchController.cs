using System;
using System.Collections.Generic;
using NextXI.Core;
using NextXI.Core.Match;
using NextXI.Core.Settings;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Everything needed to start a match.</summary>
    public sealed class MatchSetup
    {
        public TeamSheet Home, Away;
        public MatchSettings Settings;
        public int UserTeam = 0;                 // -1 = spectate
        public string LockedPlayerProfileId;     // career athlete (PlayerLock)
        public string Competition = "AMISTOSO";
        public string StadiumName = "ARENA NEXT";
        public bool Night = true;
        public bool ShowIntro = true;
        public Action<MatchReport> OnFinished;
        public Action OnQuit;
    }

    /// <summary>
    /// Runs a 3D match: owns the MatchEngine, builds the stadium and athlete views, feeds touch
    /// input to the engine, interpolates rendering between fixed steps, drives cameras, crowd,
    /// sounds, replays, HUD, pause/settings and the end-of-match flow.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        private enum State { Intro, Playing, Paused, Replay, HalfTime, FullTime }

        public MatchEngine Engine { get; private set; }
        private MatchSetup _setup;
        private StadiumBuilder _stadium;
        private CameraDirector _cam;
        private TouchControls _controls;
        private BallView _ball;
        private readonly Dictionary<int, PlayerView> _views = new Dictionary<int, PlayerView>();
        private readonly ReplaySystem _replay = new ReplaySystem(12f);
        private readonly ReplayPlayer _replayPlayer = new ReplayPlayer();
        private readonly List<MatchEvent> _events = new List<MatchEvent>();
        private readonly List<(string text, Color color, float until)> _toasts = new List<(string, Color, float)>();
        private State _state = State.Intro;
        private State _resumeState = State.Playing;
        private float _stateTime, _time;
        private float _excitement = 0.3f, _excitementSpike;
        private float _pendingReplayAt = -1f;
        private int _goalSide = 1;
        private float _nextChant;
        private bool _showSettings, _showSubs;
        private int _settingsTab;
        private GameSettings Settings => GameApp.Instance.Settings;
        private Transform _root;
        private MatchReport _report;

        public static MatchController Launch(MatchSetup setup)
        {
            var go = new GameObject("Match");
            var mc = go.AddComponent<MatchController>();
            mc.Init(setup);
            return mc;
        }

        private void Init(MatchSetup setup)
        {
            _setup = setup;
            _root = transform;
            Engine = new MatchEngine(setup.Home, setup.Away, setup.Settings);
            Engine.HoldAtHalfTime = true;
            if (setup.UserTeam >= 0) Engine.SetUserTeam(setup.UserTeam, setup.LockedPlayerProfileId);

            var g = Settings.Graphics;
            _stadium = new StadiumBuilder();
            _stadium.Build(new StadiumOptions
            {
                Pitch = Engine.Pitch,
                HomeColor = Conv.Hex(setup.Home.Kit.Shirt),
                AwayColor = Conv.Hex(setup.Away.Kit.Shirt),
                CrowdDensity = g.CrowdDensity,
                Night = setup.Night,
                StadiumName = setup.StadiumName,
                Seed = (setup.Home.ClubId ?? "x").GetHashCode()
            }, _root);

            _cam = CameraDirector.Create(_root);
            _cam.Settings = Settings.Camera;
            _cam.Mode = Settings.Camera.Mode;
            _controls = new TouchControls(Settings.Controls);
            _ball = BallView.Create(_root);
            foreach (var p in Engine.Players) EnsureView(p);

            var a = AudioService.Instance;
            if (a != null)
            {
                a.PlayMusic(false);
                float L = Engine.Pitch.HalfLength, W = Engine.Pitch.HalfWidth;
                a.StartCrowd(new[] { new Vector3(0, 10, W + 20), new Vector3(0, 10, -W - 20), new Vector3(L + 20, 10, 0), new Vector3(-L - 20, 10, 0) });
            }
            GameApp.Instance.Quality.Apply(g);
            Engine.StartMatch();
            _state = setup.ShowIntro && !Settings.Match.SkipCutscenes ? State.Intro : State.Playing;
            _stateTime = 0f;
            if (_state == State.Playing) AudioService.Instance?.Play(Sfx.WhistleLong);
            NextXI.Core.Log.Info("Match", $"Started {setup.Home.Name} x {setup.Away.Name}, {setup.Settings.RealMinutesPerHalf} min/half, {setup.Settings.Difficulty}");
        }

        private PlayerView EnsureView(MatchPlayer p)
        {
            if (_views.TryGetValue(p.Id, out var v) && v != null) return v;
            var kit = Engine.Teams[p.Team].Sheet.Kit;
            v = PlayerView.Create(p, kit, _root, Settings.Graphics.Shadows);
            v.PlayerIdIsKeeper = p.IsGoalkeeper;
            _views[p.Id] = v;
            return v;
        }

        private void OnDestroy()
        {
            AudioService.Instance?.StopCrowd();
        }

        // ================================================================== update

        private void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;
            _stateTime += Time.unscaledDeltaTime;
            GameApp.Instance.Quality.Tick(Time.unscaledDeltaTime);
            var input = _controls.Poll();
            _controls.Context = _state == State.Playing ? Engine.CurrentUserContext : UserContext.None;

            switch (_state)
            {
                case State.Intro:
                    _cam.UpdateOrbit(_time, dt, 70f, 22f);
                    RenderEngineState(dt);
                    if (_stateTime > 5f || input.AnyTouch && _stateTime > 0.5f || input.Pause || Input.anyKeyDown && _stateTime > 0.3f)
                    {
                        _state = State.Playing; _stateTime = 0; _cam.Snap();
                        AudioService.Instance?.Play(Sfx.WhistleLong);
                    }
                    break;

                case State.Playing:
                    if (input.Pause) { Pause(); break; }
                    if (input.CameraCycle) CycleCamera();
                    FeedCommand(input);
                    Engine.Advance(dt);
                    HandleEvents();
                    _replay.Record(Engine, _time, dt);
                    RenderEngineState(dt);
                    UpdateGameplayCamera(dt);
                    UpdateCrowd(dt);
                    if (_pendingReplayAt > 0f && _time >= _pendingReplayAt) StartGoalReplay();
                    if (Engine.Phase == MatchPhase.HalfTime) { _state = State.HalfTime; _stateTime = 0; }
                    if (Engine.IsFinished) FinishMatch();
                    if (Engine.UserSubbedOff && _setup.LockedPlayerProfileId != null) Engine.UserAutopilot = true;
                    break;

                case State.Replay:
                    if (!_replayPlayer.Step(Time.unscaledDeltaTime) || input.AnyTouch && _stateTime > 0.4f || input.Pause)
                    {
                        _replayPlayer.Stop();
                        _state = State.Playing;
                        _cam.Snap();
                        Engine.SkipCelebration();
                        break;
                    }
                    RenderReplay(Time.unscaledDeltaTime);
                    UpdateCrowd(dt);
                    break;

                case State.HalfTime:
                case State.FullTime:
                case State.Paused:
                    _cam.UpdateOrbit(_time * 0.5f, Time.unscaledDeltaTime, 60f, 18f);
                    UpdateCrowd(dt);
                    break;
            }
        }

        private void FeedCommand(in InputFrame f)
        {
            if (Engine.UserPlayer == null) return;
            var c = Engine.Command;
            Vector3 world = _cam.StickToWorld(f.Stick);
            c.Move = new Vec3(world.x, 0, world.z);
            c.Sprint = f.Sprint;
            c.ShieldHeld = f.ShieldHeld;
            c.JockeyHeld = f.Sprint == false && Engine.CurrentUserContext == UserContext.Defending && f.Stick.sqrMagnitude > 0.01f && f.ShieldHeld;
            // Edges accumulate until the engine consumes them in a fixed step.
            c.Pass |= f.Pass; c.Through |= f.Through; c.Lob |= f.Lob; c.Shoot |= f.Shoot; c.Skill |= f.Skill; c.Switch |= f.Switch;
            if (f.Pass || f.Through || f.Lob || f.Shoot) c.Power = f.Power;
            Engine.Command = c;
        }

        private void RenderEngineState(float dt)
        {
            float a = Engine.InterpolationAlpha;
            Vector3 ballPos = Vector3.Lerp(Engine.BallPrevPos.ToUnity(), Engine.Ball.Position.ToUnity(), a);
            _ball.Apply(ballPos, Engine.Ball.Spin.ToUnity(), Engine.Ball.Velocity.ToUnity(), dt);
            var seen = HashSetPool;
            seen.Clear();
            foreach (var p in Engine.Players)
            {
                var v = EnsureView(p);
                seen.Add(p.Id);
                var f = PlayerFrame.From(Engine, p);
                f.Pos = Vector3.Lerp(p.PrevPos.ToUnity(), p.Pos.ToUnity(), a);
                v.Apply(f, dt, _time, ballPos);
                v.SetUserMarker(p.UserControlled && p.IsActive && !Engine.UserAutopilot, Engine.Owner == p);
            }
            foreach (var kv in _views) if (!seen.Contains(kv.Key) && kv.Value.gameObject.activeSelf) kv.Value.gameObject.SetActive(false);
        }

        private static readonly HashSet<int> HashSetPool = new HashSet<int>();

        private void RenderReplay(float dt)
        {
            _replayPlayer.Sample(out var fa, out var fb, out float t);
            Vector3 ball = Vector3.Lerp(fa.BallPos, fb.BallPos, t);
            _ball.Apply(ball, fa.BallSpin, fa.BallVel, dt * _replayPlayer.Speed);
            foreach (var kv in _views)
            {
                if (!fa.Players.TryGetValue(kv.Key, out var pa) || !fb.Players.TryGetValue(kv.Key, out var pb)) { kv.Value.gameObject.SetActive(false); continue; }
                var f = PlayerFrame.Lerp(pa, pb, t);
                kv.Value.Apply(f, dt * _replayPlayer.Speed, _time, ball);
                kv.Value.SetUserMarker(false, false);
            }
            _cam.UpdateReplay(ball, _replayPlayer.Time, dt, _goalSide);
            if ((int)(_replayPlayer.Time / 2.6f) != (int)((_replayPlayer.Time - dt * _replayPlayer.Speed) / 2.6f)) _cam.NextReplayAngle();
        }

        private void UpdateGameplayCamera(float dt)
        {
            var u = Engine.UserPlayer;
            Vector3? user = u != null && u.IsActive ? u.Pos.ToUnity() : (Vector3?)null;
            int dir = Engine.UserTeam >= 0 ? Engine.AttackDir(Engine.UserTeam) : 1;
            bool setPiece = Engine.Phase == MatchPhase.SetPiece && (Engine.CurrentRestart.Type == RestartType.Penalty || Engine.CurrentRestart.Type == RestartType.FreeKick || Engine.CurrentRestart.Type == RestartType.Corner);
            Vector3 focus = Engine.Holder != null ? Engine.Holder.Pos.ToUnity() : Engine.Ball.Position.ToUnity();
            _cam.UpdateGameplay(focus, Engine.Ball.Velocity.ToUnity(), user, dir, setPiece, dt);
        }

        private void CycleCamera()
        {
            var modes = (CameraMode[])Enum.GetValues(typeof(CameraMode));
            int i = Array.IndexOf(modes, _cam.Mode);
            _cam.Mode = modes[(i + 1) % modes.Length];
            Settings.Camera.Mode = _cam.Mode;
            Toast("CÂMERA: " + CameraName(_cam.Mode), Ui.Cyan, 1.2f);
        }

        public static string CameraName(CameraMode m)
        {
            switch (m)
            {
                case CameraMode.BroadcastClose: return "TRANSMISSÃO PRÓXIMA";
                case CameraMode.PlayerCam: return "JOGADOR";
                case CameraMode.Dynamic: return "DINÂMICA";
                default: return "TRANSMISSÃO";
            }
        }

        // ================================================================== events, crowd, audio

        private void HandleEvents()
        {
            _events.Clear();
            Engine.Events.Drain(_events);
            var a = AudioService.Instance;
            foreach (var e in _events)
            {
                Vector3 at = e.Position.ToUnity();
                bool userTeam = e.Team == Engine.UserTeam;
                switch (e.Type)
                {
                    case MatchEventType.Pass:
                    case MatchEventType.Cross:
                        a?.Play(e.Value > 18f ? Sfx.KickHard : Sfx.KickSoft, at, 0.8f);
                        break;
                    case MatchEventType.BallTouch:
                        if (e.Value > 6f) a?.Play(Sfx.KickSoft, at, 0.35f, 0.12f);
                        break;
                    case MatchEventType.Shot:
                        a?.Play(Sfx.KickHard, at, 1f);
                        _excitementSpike = Mathf.Max(_excitementSpike, 0.5f);
                        break;
                    case MatchEventType.Woodwork:
                        a?.Play(Sfx.Post, at, 1f);
                        a?.Play(Sfx.NearMiss, null, 0.8f);
                        Toast("NA TRAVE!", Ui.Gold, 1.6f);
                        break;
                    case MatchEventType.Save:
                        a?.Play(Sfx.Applause, null, 0.6f);
                        Toast("DEFESA!", Ui.Cyan, 1.4f);
                        break;
                    case MatchEventType.ChanceMissed:
                        a?.Play(Sfx.NearMiss, null, 0.9f);
                        break;
                    case MatchEventType.Goal:
                    {
                        a?.Play(Sfx.Net, at, 1f);
                        a?.Play(Sfx.GoalRoar, null, e.Team == 0 ? 1f : 0.65f);
                        a?.Play(Sfx.Whistle, null, 0.8f);
                        _excitementSpike = 1f;
                        var scorer = Engine.FindPlayerAny(e.PlayerId);
                        string who = scorer != null ? scorer.Profile.ShortName.ToUpperInvariant() : "";
                        Toast(e.Value > 0.5f ? "GOL CONTRA" : "GOOOL!  " + who, Ui.Accent, 3.5f);
                        if (Settings.Controls.Vibration && userTeam) Haptics.Pulse();
                        _goalSide = (int)Mathf.Sign(e.Position.X);
                        if (Settings.Camera.AutoReplays) _pendingReplayAt = _time + 2.2f;
                        break;
                    }
                    case MatchEventType.Foul:
                        a?.Play(Sfx.Whistle, at, 0.9f);
                        a?.Play(Sfx.Tackle, at, 0.8f);
                        if (e.Team == 0) a?.Play(Sfx.Boo, null, 0.5f);
                        Toast("FALTA", Ui.Text, 1.3f);
                        break;
                    case MatchEventType.YellowCard: Toast("CARTÃO AMARELO", Ui.Gold, 2f); break;
                    case MatchEventType.RedCard: Toast("CARTÃO VERMELHO", Ui.Bad, 2.5f); a?.Play(Sfx.Boo, null, 0.8f); break;
                    case MatchEventType.Penalty: Toast("PÊNALTI!", Ui.Accent, 2.5f); _excitementSpike = 0.8f; break;
                    case MatchEventType.Offside: a?.Play(Sfx.Whistle, at, 0.8f); Toast("IMPEDIMENTO", Ui.Text, 1.5f); break;
                    case MatchEventType.Corner: Toast("ESCANTEIO", Ui.Text, 1.3f); break;
                    case MatchEventType.TackleWon: a?.Play(Sfx.Tackle, at, 0.6f); break;
                    case MatchEventType.Substitution:
                    {
                        var on = Engine.FindPlayerAny(e.PlayerId); var off = Engine.FindPlayerAny(e.OtherPlayerId);
                        if (on != null) { EnsureView(on); Toast($"SUBSTITUIÇÃO: {off?.Profile.ShortName} ⇄ {on.Profile.ShortName}", Ui.Cyan, 2.5f); }
                        break;
                    }
                    case MatchEventType.Injury: Toast("JOGADOR LESIONADO", Ui.Bad, 2f); break;
                    case MatchEventType.StoppageAnnounced: Toast($"ACRÉSCIMOS: +{(int)e.Value}", Ui.Cyan, 2.5f); break;
                    case MatchEventType.HalfTime: a?.Play(Sfx.WhistleTriple); a?.Play(Sfx.Applause, null, 0.7f); break;
                    case MatchEventType.FullTime: a?.Play(Sfx.WhistleTriple); a?.Play(Sfx.Applause, null, 0.9f); break;
                    case MatchEventType.KickOff: case MatchEventType.SecondHalf: a?.Play(Sfx.Whistle); break;
                }
            }
        }

        private void UpdateCrowd(float dt)
        {
            // Excitement: base tension, ball near a goal, close scoreline late on, spikes after events.
            float ballX = Mathf.Abs(Engine.Ball.Position.X);
            float danger = Mathf.InverseLerp(25f, 50f, ballX);
            float minute = Engine.Clock.TotalVirtualSeconds / 60f;
            int diff = Mathf.Abs(Engine.Score(0) - Engine.Score(1));
            float late = Mathf.InverseLerp(70f, 90f, minute) * (diff <= 1 ? 1f : 0.3f);
            _excitementSpike = Mathf.MoveTowards(_excitementSpike, 0f, dt * 0.12f);
            float target = Mathf.Clamp01(0.28f + danger * 0.35f + late * 0.25f + _excitementSpike);
            _excitement = Mathf.Lerp(_excitement, target, 1f - Mathf.Exp(-dt * 1.5f));
            _stadium.AnimateCrowd(_time, _excitement);
            AudioService.Instance?.SetCrowdExcitement(_excitement);
            if (_time > _nextChant)
            {
                _nextChant = _time + UnityEngine.Random.Range(25f, 50f);
                // Home fans sing more when their team leads or the game is tight.
                if (Engine.Score(0) >= Engine.Score(1)) AudioService.Instance?.Chant();
            }
        }

        private void Toast(string text, Color c, float seconds)
        {
            _toasts.Add((text, c, Time.unscaledTime + seconds));
            if (_toasts.Count > 3) _toasts.RemoveAt(0);
        }

        private void StartGoalReplay()
        {
            _pendingReplayAt = -1f;
            var frames = _replay.Extract(7.5f, 1.2f);
            if (frames.Count < 30) return;
            _replayPlayer.Speed = 0.7f;
            _replayPlayer.Start(frames);
            _state = State.Replay;
            _stateTime = 0f;
            _cam.Snap();
        }

        // ================================================================== flow

        private void Pause()
        {
            _resumeState = _state;
            _state = State.Paused;
            _stateTime = 0f;
            Time.timeScale = 0f;
            AudioListener.pause = false;
        }

        private void Resume()
        {
            _state = _resumeState == State.Paused ? State.Playing : _resumeState;
            Time.timeScale = 1f;
            _showSettings = _showSubs = false;
            GameApp.Instance.SaveSettings();
            _cam.Snap();
        }

        private void FinishMatch()
        {
            if (_state == State.FullTime) return;
            _state = State.FullTime;
            _stateTime = 0f;
            _report = MatchReport.FromEngine(Engine);
        }

        private void LeaveMatch(bool finished)
        {
            Time.timeScale = 1f;
            var setup = _setup;
            var report = finished ? (_report ?? SimulateRemainder()) : null;
            Destroy(gameObject);
            Mat.ClearCache();
            if (finished) setup.OnFinished?.Invoke(report);
            else setup.OnQuit?.Invoke();
        }

        /// <summary>Finishes the match headless (used when quitting a career match early: result still counts).</summary>
        private MatchReport SimulateRemainder()
        {
            Engine.UserAutopilot = true;
            Engine.HoldAtHalfTime = false;
            Engine.RunToEnd();
            return MatchReport.FromEngine(Engine);
        }

        // ================================================================== HUD

        private void OnGUI()
        {
            Ui.Begin();
            float W = Ui.W, H = Ui.H;
            if (_state != State.Intro) DrawScoreBug();
            switch (_state)
            {
                case State.Intro:
                    DrawIntro(W, H);
                    break;
                case State.Playing:
                    DrawToasts(W);
                    DrawUserInfo(W, H);
                    _controls.Draw();
                    if (Engine.Phase == MatchPhase.SetPiece && Engine.CurrentUserContext == UserContext.SetPiece)
                        Ui.Label(new Rect(0, H * 0.2f, W, 40), SetPieceHint(), 24, Ui.Text, TextAnchor.MiddleCenter);
                    break;
                case State.Replay:
                    Ui.Round(new Rect(24, H - 70, 170, 46), new Color(0, 0, 0, 0.5f));
                    Ui.Label(new Rect(24, H - 70, 170, 46), "● REPLAY", 26, Ui.Accent, TextAnchor.MiddleCenter, Ui.FontKind.DisplayItalic);
                    Ui.Label(new Rect(W - 300, H - 64, 280, 40), "TOQUE PARA PULAR", 20, Ui.Muted, TextAnchor.MiddleRight);
                    DrawToasts(W);
                    break;
                case State.Paused:
                    DrawPause(W, H);
                    break;
                case State.HalfTime:
                    DrawStatsPanel(W, H, "INTERVALO", "SEGUNDO TEMPO", () => { Engine.ContinueFromHalfTime(); _state = State.Playing; _cam.Snap(); });
                    break;
                case State.FullTime:
                    DrawStatsPanel(W, H, "FIM DE JOGO", "CONTINUAR", () => LeaveMatch(true));
                    break;
            }
        }

        private string SetPieceHint()
        {
            switch (Engine.CurrentRestart.Type)
            {
                case RestartType.Penalty: return "PÊNALTI — mire com o analógico e segure CHUTE";
                case RestartType.FreeKick: return "FALTA — CHUTE para bater ao gol, PASSE ou LANÇAR";
                case RestartType.Corner: return "ESCANTEIO — CRUZAR (analógico escolhe a zona) ou PASSE curto";
                case RestartType.ThrowIn: return "LATERAL — aponte e toque em PASSE";
                default: return "";
            }
        }

        private void DrawIntro(float W, float H)
        {
            float k = Mathf.Clamp01(_stateTime / 0.6f);
            Ui.Fill(new Rect(0, H - 170, W, 170), new Color(0.03f, 0.05f, 0.12f, 0.75f * k));
            Ui.Label(new Rect(40, H - 160, W - 80, 30), _setup.Competition + "  ·  " + _setup.StadiumName, 22, Ui.Accent, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
            Ui.Label(new Rect(40, H - 128, W - 80, 64), $"{_setup.Home.Name.ToUpperInvariant()}  x  {_setup.Away.Name.ToUpperInvariant()}", 54, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
            Ui.Label(new Rect(40, H - 62, W - 80, 30), $"{Engine.Settings.RealMinutesPerHalf} MIN POR TEMPO  ·  {DifficultyName(Engine.Settings.Difficulty)}  ·  TOQUE PARA COMEÇAR", 20, Ui.Muted);
            Ui.Logo(new Rect(W - 260, 24, 230, 44), k);
        }

        public static string DifficultyName(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Amateur: return "AMADOR";
                case Difficulty.Semipro: return "SEMIPROFISSIONAL";
                case Difficulty.Professional: return "PROFISSIONAL";
                case Difficulty.WorldClass: return "CLASSE MUNDIAL";
                default: return "LENDÁRIO";
            }
        }

        private void DrawScoreBug()
        {
            var r = new Rect(16, 14, 380, 50);
            Ui.Round(r, new Color(0.03f, 0.05f, 0.12f, 0.88f));
            var t0 = Engine.Teams[0]; var t1 = Engine.Teams[1];
            Ui.Round(new Rect(r.x + 8, r.y + 10, 8, 30), Conv.Hex(t0.Sheet.Kit.Shirt));
            Ui.Label(new Rect(r.x + 22, r.y, 70, r.height), t0.ShortName, 26, Ui.Text);
            Ui.Round(new Rect(r.x + 92, r.y + 6, 92, 38), new Color(1, 1, 1, 0.1f));
            Ui.Label(new Rect(r.x + 92, r.y, 92, r.height), $"{Engine.Score(0)} - {Engine.Score(1)}", 30, Ui.Text, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(r.x + 192, r.y, 70, r.height), t1.ShortName, 26, Ui.Text);
            Ui.Round(new Rect(r.x + 256, r.y + 10, 8, 30), Conv.Hex(t1.Sheet.Kit.Shirt));
            Ui.Round(new Rect(r.x + 272, r.y + 6, 100, 38), Ui.Accent);
            Ui.Label(new Rect(r.x + 272, r.y, 100, r.height), Engine.Clock.Display, 26, Ui.Text, TextAnchor.MiddleCenter);
            if (Engine.Clock.StoppageAnnounced && Engine.Clock.Period != MatchPeriod.HalfTime)
            {
                Ui.Round(new Rect(r.xMax + 6, r.y + 8, 52, 34), Ui.Cyan);
                Ui.Label(new Rect(r.xMax + 6, r.y + 8, 52, 34), "+" + Engine.Clock.StoppageMinutes, 24, Ui.Bg, TextAnchor.MiddleCenter);
            }
        }

        private void DrawToasts(float W)
        {
            float y = 84;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var (text, c, until) = _toasts[i];
                if (Time.unscaledTime > until) { _toasts.RemoveAt(i); continue; }
                float alpha = Mathf.Clamp01((until - Time.unscaledTime) / 0.3f);
                var r = new Rect(W * 0.5f - 260, y, 520, 54);
                Ui.Round(r, new Color(0.03f, 0.05f, 0.12f, 0.8f * alpha));
                Ui.Round(new Rect(r.x, r.y, 6, r.height), new Color(c.r, c.g, c.b, alpha));
                Ui.Label(r, text, 32, new Color(c.r, c.g, c.b, alpha), TextAnchor.MiddleCenter, Ui.FontKind.DisplayItalic);
                y += 60;
            }
        }

        private void DrawUserInfo(float W, float H)
        {
            var u = Engine.UserPlayer;
            if (u == null) return;
            var r = new Rect(16, 72, 300, 40);
            Ui.Round(r, new Color(0.03f, 0.05f, 0.12f, 0.6f));
            Ui.Label(new Rect(r.x + 12, r.y, 200, r.height), $"#{u.ShirtNumber} {u.Profile.ShortName.ToUpperInvariant()}", 22, Ui.Text);
            var bar = new Rect(r.x + 200, r.y + 16, 88, 8);
            Ui.Round(bar, new Color(1, 1, 1, 0.15f));
            Ui.Round(new Rect(bar.x, bar.y, bar.width * u.Stamina, bar.height), u.Stamina > 0.5f ? Ui.Good : u.Stamina > 0.3f ? Ui.Gold : Ui.Bad);
            if (Engine.UserAutopilot) Ui.Label(new Rect(16, 116, 400, 30), "SUBSTITUÍDO — partida em simulação", 20, Ui.Muted);
        }

        private void DrawPause(float W, float H)
        {
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.03f, 0.08f, 0.82f));
            if (_showSettings) { if (SettingsScreen.Draw(W, H, ref _settingsTab, Settings, () => _showSettings = false)) GameApp.Instance.ApplySettings(); _controls.Settings = Settings.Controls; _cam.Settings = Settings.Camera; return; }
            if (_showSubs) { DrawSubs(W, H); return; }
            Ui.Label(new Rect(0, 60, W, 70), "PAUSA", 64, Ui.Text, TextAnchor.MiddleCenter, Ui.FontKind.DisplayItalic);
            float bw = 360, bh = 64, x = W * 0.5f - bw * 0.5f, y = 160;
            if (Ui.Button(new Rect(x, y, bw, bh), "CONTINUAR", true, 28)) Resume();
            if (Ui.Button(new Rect(x, y + 76, bw, bh), "CÂMERA: " + CameraName(_cam.Mode), false, 22)) CycleCamera();
            if (Engine.UserTeam >= 0 && Engine.Settings.ControlMode == ControlMode.TeamSwitching && Ui.Button(new Rect(x, y + 152, bw, bh), "SUBSTITUIÇÕES", false, 24)) _showSubs = true;
            if (Ui.Button(new Rect(x, y + 228, bw, bh), "CONFIGURAÇÕES", false, 24)) _showSettings = true;
            bool career = _setup.LockedPlayerProfileId != null;
            if (Ui.Button(new Rect(x, y + 304, bw, bh), career ? "SIMULAR ATÉ O FIM" : "SAIR DA PARTIDA", false, 24))
            {
                if (career) LeaveMatch(true); else LeaveMatch(false);
            }
            DrawStatsMini(new Rect(W * 0.5f + 220, 160, 300, 300));
        }

        private void DrawSubs(float W, float H)
        {
            int t = Engine.UserTeam;
            var team = Engine.Teams[t];
            Ui.Label(new Rect(0, 30, W, 50), $"SUBSTITUIÇÕES ({team.SubstitutionsUsed}/{Engine.Settings.MaxSubstitutions})", 40, Ui.Text, TextAnchor.MiddleCenter, Ui.FontKind.DisplayItalic);
            float x0 = W * 0.5f - 470, y = 100;
            for (int i = 0; i < team.Players.Count; i++)
            {
                var p = team.Players[i];
                var r = new Rect(x0, y + i * 46, 440, 42);
                Ui.PanelBox(r, _subOff == p.Id);
                Ui.Label(new Rect(r.x + 10, r.y, 260, r.height), $"{p.Position}  #{p.ShirtNumber} {p.Profile.ShortName}", 20, p.IsActive ? Ui.Text : Ui.Muted);
                var bar = new Rect(r.x + 300, r.y + 17, 120, 8);
                Ui.Round(bar, new Color(1, 1, 1, 0.12f));
                Ui.Round(new Rect(bar.x, bar.y, bar.width * p.Stamina, 8), p.Stamina > 0.5f ? Ui.Good : Ui.Gold);
                if (p.IsActive && GUI.Button(Ui.P(r), GUIContent.none, GUIStyle.none)) _subOff = p.Id;
            }
            for (int i = 0; i < team.Bench.Count; i++)
            {
                var p = team.Bench[i];
                var r = new Rect(x0 + 500, y + i * 46, 440, 42);
                Ui.PanelBox(r);
                Ui.Label(new Rect(r.x + 10, r.y, 330, r.height), $"{p.Profile.Position}  #{p.ShirtNumber} {p.Profile.ShortName}  ({p.Profile.Overall})", 20, Ui.Text);
                if (_subOff >= 0 && Ui.Button(new Rect(r.xMax - 92, r.y + 4, 86, 34), "ENTRAR", true, 18))
                {
                    if (Engine.RequestSubstitution(t, _subOff, i)) Toast("SUBSTITUIÇÃO NA PRÓXIMA PARALISAÇÃO", Ui.Cyan, 2f);
                    _subOff = -1;
                }
            }
            if (Ui.Button(new Rect(W * 0.5f - 150, H - 80, 300, 56), "VOLTAR", false, 24)) _showSubs = false;
        }

        private int _subOff = -1;

        private void DrawStatsMini(Rect r)
        {
            Ui.PanelBox(r);
            var s0 = Engine.Teams[0].Stats; var s1 = Engine.Teams[1].Stats;
            float tot = Mathf.Max(1f, s0.PossessionSeconds + s1.PossessionSeconds);
            string[] rows =
            {
                $"{Mathf.RoundToInt(100 * s0.PossessionSeconds / tot)}%|POSSE|{Mathf.RoundToInt(100 * s1.PossessionSeconds / tot)}%",
                $"{s0.Shots}|FINALIZAÇÕES|{s1.Shots}",
                $"{s0.ShotsOnTarget}|NO GOL|{s1.ShotsOnTarget}",
                $"{s0.PassesCompleted}/{s0.Passes}|PASSES|{s1.PassesCompleted}/{s1.Passes}",
                $"{s0.Corners}|ESCANTEIOS|{s1.Corners}",
                $"{s0.Fouls}|FALTAS|{s1.Fouls}",
                $"{s0.XG:0.0}|xG|{s1.XG:0.0}",
            };
            for (int i = 0; i < rows.Length; i++)
            {
                var parts = rows[i].Split('|');
                float y = r.y + 14 + i * 38;
                Ui.Label(new Rect(r.x + 14, y, 80, 34), parts[0], 22, Ui.Text);
                Ui.Label(new Rect(r.x, y, r.width, 34), parts[1], 18, Ui.Muted, TextAnchor.MiddleCenter);
                Ui.Label(new Rect(r.xMax - 94, y, 80, 34), parts[2], 22, Ui.Text, TextAnchor.MiddleRight);
            }
        }

        private void DrawStatsPanel(float W, float H, string title, string button, Action onContinue)
        {
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.03f, 0.08f, 0.78f));
            Ui.Label(new Rect(0, 30, W, 60), title, 56, Ui.Text, TextAnchor.MiddleCenter, Ui.FontKind.DisplayItalic);
            Ui.Label(new Rect(0, 92, W, 60), $"{Engine.Teams[0].Name.ToUpperInvariant()}  {Engine.Score(0)} - {Engine.Score(1)}  {Engine.Teams[1].Name.ToUpperInvariant()}", 40, Ui.Accent, TextAnchor.MiddleCenter);
            DrawStatsMini(new Rect(W * 0.5f - 460, 170, 420, 300));
            // Goal list and ratings of the user's side.
            var r = new Rect(W * 0.5f + 20, 170, 440, 300);
            Ui.PanelBox(r);
            float y = r.y + 12;
            foreach (var e in Engine.Events.All)
            {
                if (e.Type != MatchEventType.Goal || y > r.yMax - 40) continue;
                var p = Engine.FindPlayerAny(e.PlayerId);
                Ui.Label(new Rect(r.x + 14, y, r.width - 28, 30), $"{e.Minute}'  {p?.Profile.ShortName}  ({Engine.Teams[e.Team].ShortName}){(e.Value > 0.5f ? " (contra)" : "")}", 20, Ui.Text);
                y += 30;
            }
            if (_state == State.FullTime && _report != null && Engine.UserPlayer != null)
            {
                var line = _report.Line(Engine.UserPlayer.Profile.Id);
                if (line != null)
                    Ui.Label(new Rect(r.x + 14, r.yMax - 44, r.width - 28, 34), $"SUA NOTA: {line.Rating:0.0}   G {line.Goals}  A {line.Assists}  PASSES {line.PassesCompleted}/{line.Passes}", 22, Ui.Gold);
            }
            if (Ui.Button(new Rect(W * 0.5f - 170, H - 100, 340, 64), button, true, 28)) onContinue();
        }
    }
}
