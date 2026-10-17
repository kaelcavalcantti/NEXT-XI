using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core;
using NextXI.Core.Career;
using NextXI.Core.Data;
using NextXI.Core.Match;
using NextXI.Core.Settings;
using NextXI.Core.World;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>
    /// Plays a training drill in 3D: the DrillRun (core) owns the rules and the engine; this
    /// component renders it, feeds touch input and shows objectives, attempts and the score.
    /// </summary>
    public sealed class TrainingController : MonoBehaviour
    {
        private DrillRun _run;
        private CameraDirector _cam;
        private TouchControls _controls;
        private BallView _ball;
        private readonly Dictionary<int, PlayerView> _views = new Dictionary<int, PlayerView>();
        private readonly List<GameObject> _markers = new List<GameObject>();
        private Action<float> _onDone;
        private Action _onQuit;
        private float _time, _acc;
        private bool _paused;
        private string _drillName;

        public static TrainingController Launch(string drillId, PlayerProfile athlete, TeamSheet own, TeamSheet sparring, ulong seed, Action<float> onDone, Action onQuit)
        {
            var go = new GameObject("Training");
            var tc = go.AddComponent<TrainingController>();
            tc.Init(drillId, athlete, own, sparring, seed, onDone, onQuit);
            return tc;
        }

        private void Init(string drillId, PlayerProfile athlete, TeamSheet own, TeamSheet sparring, ulong seed, Action<float> onDone, Action onQuit)
        {
            _onDone = onDone;
            _onQuit = onQuit;
            var gs = GameApp.Instance.Settings;
            _run = new DrillRun(drillId, athlete, own, sparring, seed, gs.Match.Difficulty);
            if (_run.Simulated) _run.Engine.UserAutopilot = true;
            _drillName = CareerManager.Drills.FirstOrDefault(d => d.Id == drillId)?.Name ?? drillId;
            new StadiumBuilder().Build(new StadiumOptions { Pitch = _run.Engine.Pitch, CrowdDensity = 0, Night = false }, transform);
            _cam = CameraDirector.Create(transform);
            _cam.Settings = gs.Camera;
            _cam.Mode = CameraMode.PlayerCam;
            _controls = new TouchControls(gs.Controls);
            _ball = BallView.Create(transform);
            foreach (var p in _run.Engine.Players.Where(p => p.IsActive))
            {
                var v = PlayerView.Create(p, _run.Engine.Teams[p.Team].Sheet.Kit, transform, gs.Graphics.Shadows);
                v.PlayerIdIsKeeper = p.IsGoalkeeper;
                _views[p.Id] = v;
            }
            AudioService.Instance?.PlayMusic(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;
            var input = _controls.Poll();
            var e = _run.Engine;
            _controls.Context = _run.Finished || _paused || _run.Simulated ? UserContext.None : e.CurrentUserContext;
            if (input.Pause) _paused = !_paused;
            if (!_paused && !_run.Finished)
            {
                // Feed input and run fixed steps through the drill (it consumes the engine steps).
                var c = e.Command;
                Vector3 world = _cam.StickToWorld(input.Stick);
                c.Move = new Vec3(world.x, 0, world.z);
                c.Sprint = input.Sprint;
                c.ShieldHeld = input.ShieldHeld;
                c.Pass |= input.Pass; c.Through |= input.Through; c.Lob |= input.Lob; c.Shoot |= input.Shoot; c.Skill |= input.Skill;
                if (input.Pass || input.Through || input.Lob || input.Shoot) c.Power = input.Power;
                e.Command = c;
                _acc += Mathf.Min(dt, 0.1f);
                while (_acc >= MatchEngine.Dt) { _run.Step(); _acc -= MatchEngine.Dt; }
                PlaySounds();
            }
            Render(dt);
            var u = _run.User;
            _cam.UpdateGameplay(e.Ball.Position.ToUnity(), e.Ball.Velocity.ToUnity(), u.Pos.ToUnity(), e.AttackDir(0), false, dt);
        }

        private readonly List<MatchEvent> _evs = new List<MatchEvent>();

        private void PlaySounds()
        {
            // Engine events were drained by the drill; use ball state changes for touch sounds instead.
            var e = _run.Engine;
            if (e.Ball.Velocity.Length > 14f && (e.Ball.Velocity - _lastBallVel).Length > 8f)
                AudioService.Instance?.Play(Sfx.KickHard, e.Ball.Position.ToUnity(), 0.8f);
            else if ((e.Ball.Velocity - _lastBallVel).Length > 5f)
                AudioService.Instance?.Play(Sfx.KickSoft, e.Ball.Position.ToUnity(), 0.6f);
            _lastBallVel = e.Ball.Velocity;
        }

        private Vec3 _lastBallVel;

        private void Render(float dt)
        {
            var e = _run.Engine;
            Vector3 ball = e.Ball.Position.ToUnity();
            _ball.Apply(ball, e.Ball.Spin.ToUnity(), e.Ball.Velocity.ToUnity(), dt);
            foreach (var p in e.Players)
            {
                if (!_views.TryGetValue(p.Id, out var v)) continue;
                var f = PlayerFrame.From(e, p);
                v.Apply(f, dt, _time, ball);
                v.SetUserMarker(p == _run.User, e.Owner == p);
            }
            // Target markers (gates, cones, zones).
            while (_markers.Count < _run.Targets.Count)
            {
                var b = new MeshBuilder();
                MeshKit.Cylinder(b, 0, Vector3.zero, Vector3.up * 1.1f, 0.12f, 10);
                var go = MeshKitUnity.Spawn("Cone", b.Build("Cone"), new[] { Mat.Opaque("cone", new Color(1f, 0.45f, 0.1f), 0.4f) }, transform, true, false);
                _markers.Add(go);
            }
            for (int i = 0; i < _markers.Count; i++)
            {
                bool on = i < _run.Targets.Count && i >= _run.TargetIndex;
                _markers[i].SetActive(on);
                if (on) _markers[i].transform.position = _run.Targets[i].ToUnity();
            }
        }

        private void OnGUI()
        {
            Ui.Begin();
            float W = Ui.W, H = Ui.H;
            var r = new Rect(16, 14, 520, 92);
            Ui.Round(r, new Color(0.03f, 0.05f, 0.12f, 0.85f));
            Ui.Label(new Rect(r.x + 14, r.y + 4, r.width, 34), "TREINO · " + _drillName.ToUpperInvariant(), 26, Ui.Accent, TextAnchor.MiddleLeft, Ui.FontKind.DisplayItalic);
            Ui.Label(new Rect(r.x + 14, r.y + 36, r.width - 28, 26), _run.Instructions + (_run.Simulated ? " (goleiro controlado pela IA com seus atributos)" : ""), 16, Ui.Text, TextAnchor.MiddleLeft, Ui.FontKind.Body);
            Ui.Label(new Rect(r.x + 14, r.y + 62, r.width - 28, 26), $"{_run.Status}  ·  acertos {_run.Successes}  ·  tempo {Mathf.Max(0, _run.AttemptLimit - _run.AttemptTime):0.0}s", 18, Ui.Cyan);
            if (!_run.Finished) _controls.Draw();
            if (_paused && !_run.Finished)
            {
                Ui.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.03f, 0.08f, 0.8f));
                Ui.Label(new Rect(0, 120, W, 60), "PAUSA", 56, Ui.Text, TextAnchor.MiddleCenter, Ui.FontKind.DisplayItalic);
                if (Ui.Button(new Rect(W * 0.5f - 160, 220, 320, 64), "CONTINUAR", true, 26)) _paused = false;
                if (Ui.Button(new Rect(W * 0.5f - 160, 300, 320, 64), "ABANDONAR TREINO", false, 22)) Leave(false);
            }
            if (_run.Finished)
            {
                Ui.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.03f, 0.08f, 0.75f));
                Ui.Label(new Rect(0, 120, W, 60), "TREINO CONCLUÍDO", 52, Ui.Text, TextAnchor.MiddleCenter, Ui.FontKind.DisplayItalic);
                Ui.Label(new Rect(0, 190, W, 80), $"{_run.Score * 100:0}", 80, Ui.Accent, TextAnchor.MiddleCenter, Ui.FontKind.DisplayItalic);
                Ui.Label(new Rect(0, 270, W, 30), $"{_run.Successes}/{_run.Attempts} tentativas bem-sucedidas", 22, Ui.Muted, TextAnchor.MiddleCenter);
                if (Ui.Button(new Rect(W * 0.5f - 160, H - 140, 320, 70), "CONCLUIR", true, 28)) Leave(true);
            }
        }

        private void Leave(bool done)
        {
            float score = _run.Score;
            var onDone = _onDone; var onQuit = _onQuit;
            Destroy(gameObject);
            Mat.ClearCache();
            if (done) onDone?.Invoke(score); else onQuit?.Invoke();
        }
    }

    /// <summary>Training menu outside the career (free practice with a demo athlete, no progression).</summary>
    public sealed class TrainingMenuScreen : IScreen
    {
        private readonly CareerState _career;
        public TrainingMenuScreen(CareerState career) { _career = career; }
        public void OnEnter() { }
        public void OnExit() { }
        public void Tick(float dt) { }

        public void Draw(float W, float H)
        {
            CareerScreens.Header(W, "TREINO LIVRE", "Pratique os fundamentos com um atleta de demonstração. Na carreira, os treinos fazem seu atleta evoluir.");
            float cw = (W - 100) / 3f;
            var drills = CareerManager.Drills;
            for (int i = 0; i < drills.Count; i++)
            {
                var d = drills[i];
                var r = new Rect(40 + (i % 3) * (cw + 10), 110 + (i / 3) * 120, cw, 110);
                Ui.PanelBox(r, true);
                Ui.Label(new Rect(r.x + 14, r.y + 6, r.width - 28, 30), d.Name.ToUpperInvariant(), 24, Ui.Text);
                Ui.Label(new Rect(r.x + 14, r.y + 36, r.width - 160, 60), d.Description, 15, Ui.Muted, TextAnchor.UpperLeft, Ui.FontKind.Body, true);
                if (Ui.Button(new Rect(r.xMax - 130, r.yMax - 54, 116, 44), "JOGAR", true, 18))
                    GameApp.Instance.StartTraining(d.Id, null);
            }
            if (Ui.Button(new Rect(40, H - 90, 240, 60), "VOLTAR")) GameApp.Instance.SetScreen(new MainMenuScreen());
        }
    }
}
