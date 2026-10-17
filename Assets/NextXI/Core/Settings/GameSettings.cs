using System;
using NextXI.Core.Match;

namespace NextXI.Core.Settings
{
    public enum QualityProfile { Economy, Balanced, High, Custom }
    public enum CameraMode { Broadcast, BroadcastClose, PlayerCam, Dynamic }

    [Serializable]
    public sealed class AudioSettings
    {
        public float Master = 0.9f, Music = 0.6f, Ambience = 0.8f, Crowd = 0.85f, Effects = 0.9f, Commentary = 0.8f, Interface = 0.7f;
        public bool Muted;

        public void Clamp()
        {
            Master = MathUtil.Clamp01(Master); Music = MathUtil.Clamp01(Music); Ambience = MathUtil.Clamp01(Ambience);
            Crowd = MathUtil.Clamp01(Crowd); Effects = MathUtil.Clamp01(Effects); Commentary = MathUtil.Clamp01(Commentary);
            Interface = MathUtil.Clamp01(Interface);
        }
    }

    [Serializable]
    public sealed class GraphicsSettings
    {
        public QualityProfile Profile = QualityProfile.Balanced;
        /// <summary>30 or 60.</summary>
        public int TargetFps = 30;
        public float RenderScale = 0.85f;
        public bool Shadows = true;
        public int ShadowDistance = 60;
        public int CrowdDensity = 2; // 0..3
        public bool PostProcessing = false;
        public int AntiAliasing = 2; // 0,2,4
        public bool DynamicResolution = true;

        public void ApplyProfile(QualityProfile p)
        {
            Profile = p;
            switch (p)
            {
                case QualityProfile.Economy:
                    TargetFps = 30; RenderScale = 0.7f; Shadows = false; ShadowDistance = 0; CrowdDensity = 1; PostProcessing = false; AntiAliasing = 0; DynamicResolution = true; break;
                case QualityProfile.Balanced:
                    TargetFps = 30; RenderScale = 0.85f; Shadows = true; ShadowDistance = 50; CrowdDensity = 2; PostProcessing = false; AntiAliasing = 2; DynamicResolution = true; break;
                case QualityProfile.High:
                    TargetFps = 60; RenderScale = 1f; Shadows = true; ShadowDistance = 90; CrowdDensity = 3; PostProcessing = true; AntiAliasing = 4; DynamicResolution = true; break;
            }
        }

        public void Clamp()
        {
            TargetFps = TargetFps >= 45 ? 60 : 30;
            RenderScale = MathUtil.Clamp(RenderScale, 0.5f, 1f);
            ShadowDistance = MathUtil.Clamp(ShadowDistance, 0, 150);
            CrowdDensity = MathUtil.Clamp(CrowdDensity, 0, 3);
            AntiAliasing = AntiAliasing >= 4 ? 4 : AntiAliasing >= 2 ? 2 : 0;
        }
    }

    [Serializable]
    public sealed class CameraSettings
    {
        public CameraMode Mode = CameraMode.Broadcast;
        public float Height = 0.5f;   // 0..1
        public float Distance = 0.5f; // 0..1
        public float Zoom = 0.5f;     // 0..1 (field of view)
        public float Angle = 0.5f;    // 0..1 pitch tilt
        public float Smoothing = 0.5f;
        public CameraMode SetPieceMode = CameraMode.PlayerCam;
        public bool AutoReplays = true;

        public void Clamp()
        {
            Height = MathUtil.Clamp01(Height); Distance = MathUtil.Clamp01(Distance); Zoom = MathUtil.Clamp01(Zoom);
            Angle = MathUtil.Clamp01(Angle); Smoothing = MathUtil.Clamp01(Smoothing);
        }
    }

    [Serializable]
    public sealed class ControlSettings
    {
        /// <summary>Global scale of on-screen controls (0.7..1.5).</summary>
        public float ButtonScale = 1f;
        public float StickScale = 1f;
        /// <summary>Stick dead zone and response.</summary>
        public float Sensitivity = 0.5f;
        public float PassAssist = 0.7f;
        public float ShotAssist = 0.5f;
        public bool Vibration = true;
        public bool FloatingStick = true;
        public bool LeftHanded;
        /// <summary>Optional custom positions (normalised screen coords, -1 = default).</summary>
        public float[] ButtonOffsetsX = new float[8];
        public float[] ButtonOffsetsY = new float[8];
        public bool HighContrastButtons;
        public bool ShowButtonLabels = true;

        public void Clamp()
        {
            ButtonScale = MathUtil.Clamp(ButtonScale, 0.7f, 1.5f);
            StickScale = MathUtil.Clamp(StickScale, 0.7f, 1.5f);
            Sensitivity = MathUtil.Clamp01(Sensitivity);
            PassAssist = MathUtil.Clamp01(PassAssist);
            ShotAssist = MathUtil.Clamp01(ShotAssist);
            if (ButtonOffsetsX == null || ButtonOffsetsX.Length != 8) ButtonOffsetsX = new float[8];
            if (ButtonOffsetsY == null || ButtonOffsetsY.Length != 8) ButtonOffsetsY = new float[8];
        }
    }

    [Serializable]
    public sealed class MatchDefaults
    {
        public int MinutesPerHalf = 5;
        public Difficulty Difficulty = Difficulty.Professional;
        public float GameSpeed = 1f;
        public bool Offside = true;
        public bool Fouls = true;
        public bool Injuries = true;
        public bool Fatigue = true;
        public bool SkipCutscenes;
    }

    /// <summary>All user preferences, persisted as one versioned save file.</summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const int Version = 1;
        public AudioSettings Audio = new AudioSettings();
        public GraphicsSettings Graphics = new GraphicsSettings();
        public CameraSettings Camera = new CameraSettings();
        public ControlSettings Controls = new ControlSettings();
        public MatchDefaults Match = new MatchDefaults();
        public string Language = "pt-BR";
        public bool SubtitlesLarge;
        public bool ColorblindKits;

        public void Validate()
        {
            Audio = Audio ?? new AudioSettings(); Audio.Clamp();
            Graphics = Graphics ?? new GraphicsSettings(); Graphics.Clamp();
            Camera = Camera ?? new CameraSettings(); Camera.Clamp();
            Controls = Controls ?? new ControlSettings(); Controls.Clamp();
            Match = Match ?? new MatchDefaults();
            if (Array.IndexOf(MatchSettings.HalfLengthOptions, Match.MinutesPerHalf) < 0) Match.MinutesPerHalf = 5;
            Match.GameSpeed = MathUtil.Clamp(Match.GameSpeed, 0.85f, 1.15f);
        }

        public MatchSettings ToMatchSettings(ulong seed)
        {
            var s = new MatchSettings
            {
                RealMinutesPerHalf = Match.MinutesPerHalf,
                Difficulty = Match.Difficulty,
                GameSpeed = Match.GameSpeed,
                OffsideEnabled = Match.Offside,
                FoulsEnabled = Match.Fouls,
                InjuriesEnabled = Match.Injuries,
                FatigueEnabled = Match.Fatigue,
                PassAssist = Controls.PassAssist,
                ShotAssist = Controls.ShotAssist,
                Seed = seed
            };
            s.Validate();
            return s;
        }
    }
}
