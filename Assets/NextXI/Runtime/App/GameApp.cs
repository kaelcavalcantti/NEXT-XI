using System;
using System.IO;
using NextXI.Core;
using NextXI.Core.Persistence;
using NextXI.Core.Settings;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>A full-screen UI state (menu, setup, career hub...).</summary>
    public interface IScreen
    {
        void OnEnter();
        void OnExit();
        void Tick(float dt);
        void Draw(float w, float h);
    }

    /// <summary>
    /// Application root (created automatically on startup, survives scene loads): logging,
    /// settings persistence, audio, quality, screen flow and the menu backdrop stadium.
    /// Matches are spawned as MatchController objects; screens are immediate-mode UI states.
    /// </summary>
    public sealed class GameApp : MonoBehaviour
    {
        public static GameApp Instance { get; private set; }
        public GameSettings Settings { get; private set; } = new GameSettings();
        public SaveSystem Saves { get; private set; }
        public QualityService Quality { get; } = new QualityService();
        public IScreen Screen { get; private set; }
        public MatchController ActiveMatch { get; private set; }
        public string Version => SaveSystem.GameVersion;

        private Transform _backdrop;
        private CameraDirector _menuCam;
        private float _time;
        private string _loadingText;
        private int _loadingFrames;
        private Action _afterLoading;

        private const string SettingsFile = "settings.json";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("NEXT XI");
            DontDestroyOnLoad(go);
            go.AddComponent<GameApp>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            NextXI.Core.Log.AddSink(new UnityLogSink());
            Application.logMessageReceived += OnUnityLog;
            UnityEngine.Screen.orientation = ScreenOrientation.LandscapeLeft;
            UnityEngine.Screen.autorotateToPortrait = false;
            UnityEngine.Screen.autorotateToPortraitUpsideDown = false;
            UnityEngine.Screen.autorotateToLandscapeLeft = true;
            UnityEngine.Screen.autorotateToLandscapeRight = true;

            string root = Path.Combine(Application.persistentDataPath, "saves");
            Saves = new SaveSystem(new DiskFileStore(root));
            LoadSettings();
            AudioService.Create().Apply(Settings.Audio);
            Quality.Apply(Settings.Graphics);
            NextXI.Core.Log.Info("App", $"NEXT XI {Version} started. Saves: {root}");
            ShowMenuBackdrop(true);
            SetScreen(new MainMenuScreen());
        }

        private static void OnUnityLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Exception)
                // Keep a persistent crash log for diagnostics (rotated at 256 KB).
                try
                {
                    string p = Path.Combine(Application.persistentDataPath, "nextxi_errors.log");
                    if (File.Exists(p) && new FileInfo(p).Length > 256 * 1024) File.Delete(p);
                    File.AppendAllText(p, $"[{DateTime.UtcNow:o}] {condition}\n{stack}\n");
                }
                catch { /* never throw from the log handler */ }
        }

        // ------------------------------------------------------------------ settings

        private void LoadSettings()
        {
            var status = Saves.TryLoad<GameSettings>(SettingsFile, "settings", GameSettings.Version, out var s, out var err);
            if (status == LoadStatus.Ok || status == LoadStatus.RecoveredFromBackup) Settings = s;
            else
            {
                if (status != LoadStatus.NotFound) NextXI.Core.Log.Warn("App", "Settings reset: " + err);
                Settings = new GameSettings();
                // First run: choose a profile from the device class.
                Settings.Graphics.ApplyProfile(SystemInfo.systemMemorySize >= 6000 && SystemInfo.processorCount >= 8 ? QualityProfile.High :
                                               SystemInfo.systemMemorySize >= 3000 ? QualityProfile.Balanced : QualityProfile.Economy);
            }
            Settings.Validate();
        }

        public void SaveSettings()
        {
            try { Settings.Validate(); Saves.Save(SettingsFile, "settings", GameSettings.Version, Settings); }
            catch (Exception e) { NextXI.Core.Log.Exception("App", e); }
        }

        public void ApplySettings()
        {
            Settings.Validate();
            AudioService.Instance?.Apply(Settings.Audio);
            Quality.Apply(Settings.Graphics);
        }

        // ------------------------------------------------------------------ screens & flow

        public void SetScreen(IScreen s)
        {
            Screen?.OnExit();
            Screen = s;
            Screen?.OnEnter();
        }

        /// <summary>Shows a loading card for a couple of frames, then runs the heavy action.</summary>
        public void RunWithLoading(string text, Action action)
        {
            _loadingText = text;
            _loadingFrames = 2;
            _afterLoading = action;
        }

        public void StartMatch(MatchSetup setup)
        {
            RunWithLoading("PREPARANDO O ESTÁDIO", () =>
            {
                SetScreen(null);
                ShowMenuBackdrop(false);
                var onFinish = setup.OnFinished;
                var onQuit = setup.OnQuit;
                setup.OnFinished = r => { ActiveMatch = null; ShowMenuBackdrop(true); onFinish?.Invoke(r); };
                setup.OnQuit = () => { ActiveMatch = null; ShowMenuBackdrop(true); onQuit?.Invoke(); if (Screen == null) SetScreen(new MainMenuScreen()); };
                ActiveMatch = MatchController.Launch(setup);
            });
        }

        private void ShowMenuBackdrop(bool on)
        {
            if (!on)
            {
                if (_backdrop != null) Destroy(_backdrop.gameObject);
                _backdrop = null;
                AudioService.Instance?.PlayMusic(false);
                return;
            }
            if (_backdrop != null) return;
            _backdrop = new GameObject("MenuBackdrop").transform;
            var sb = new StadiumBuilder();
            sb.Build(new StadiumOptions { CrowdDensity = Math.Min(1, Settings.Graphics.CrowdDensity), Night = true }, _backdrop);
            _menuCam = CameraDirector.Create(_backdrop);
            AudioService.Instance?.PlayMusic(true);
        }

        private void Update()
        {
            _time += Time.unscaledDeltaTime;
            if (_menuCam != null && ActiveMatch == null) _menuCam.UpdateOrbit(_time, Time.unscaledDeltaTime);
            if (_loadingFrames > 0)
            {
                if (--_loadingFrames == 0)
                {
                    var a = _afterLoading; _afterLoading = null;
                    try { a?.Invoke(); }
                    catch (Exception e) { NextXI.Core.Log.Exception("App", e); SetScreen(new MessageScreen("ERRO", "Não foi possível iniciar: " + e.Message, () => SetScreen(new MainMenuScreen()))); ShowMenuBackdrop(true); }
                    _loadingText = null;
                }
            }
            Screen?.Tick(Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (Screen == null && _loadingText == null) return;
            Ui.Begin();
            float W = Ui.W, H = Ui.H;
            if (Screen != null)
            {
                // Cinematic vignette over the backdrop.
                Ui.Gradient(new Rect(0, 0, W, H), new Color(0.02f, 0.03f, 0.09f, 0.55f), new Color(0.02f, 0.03f, 0.09f, 0.92f));
                try { Screen.Draw(W, H); }
                catch (Exception e) { NextXI.Core.Log.Exception("UI", e); }
            }
            if (_loadingText != null)
            {
                Ui.Fill(new Rect(0, 0, W, H), Ui.Bg);
                Ui.Logo(new Rect(W * 0.5f - 170, H * 0.5f - 70, 340, 64));
                Ui.Label(new Rect(0, H * 0.5f + 10, W, 40), _loadingText, 24, Ui.Muted, TextAnchor.MiddleCenter);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveSettings();
        }
    }

    /// <summary>Simple message/confirmation screen.</summary>
    public sealed class MessageScreen : IScreen
    {
        private readonly string _title, _body;
        private readonly Action _ok;
        public MessageScreen(string title, string body, Action ok) { _title = title; _body = body; _ok = ok; }
        public void OnEnter() { }
        public void OnExit() { }
        public void Tick(float dt) { }
        public void Draw(float W, float H)
        {
            var r = new Rect(W * 0.5f - 320, H * 0.5f - 160, 640, 320);
            Ui.PanelBox(r, true);
            Ui.Label(new Rect(r.x, r.y + 20, r.width, 50), _title, 40, Ui.Text, TextAnchor.MiddleCenter, Ui.FontKind.DisplayItalic);
            Ui.Label(new Rect(r.x + 30, r.y + 80, r.width - 60, 140), _body, 20, Ui.Muted, TextAnchor.UpperCenter, Ui.FontKind.Body, true);
            if (Ui.Button(new Rect(r.x + r.width * 0.5f - 120, r.yMax - 80, 240, 56), "OK", true)) _ok?.Invoke();
        }
    }
}
