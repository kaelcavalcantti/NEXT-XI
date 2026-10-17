using System;

namespace NextXI.Core.Match
{
    public enum Difficulty { Amateur, Semipro, Professional, WorldClass, Legendary }

    public enum ControlMode
    {
        /// <summary>User controls one fixed athlete for the whole match (career mode).</summary>
        PlayerLock,
        /// <summary>User controls the team, switching to the most relevant player.</summary>
        TeamSwitching
    }

    [Serializable]
    public sealed class MatchSettings
    {
        /// <summary>Allowed real-time minutes per half.</summary>
        public static readonly int[] HalfLengthOptions = { 3, 4, 5, 6, 8, 10 };

        public int RealMinutesPerHalf = 5;
        public Difficulty Difficulty = Difficulty.Professional;
        public bool OffsideEnabled = true;
        public bool FoulsEnabled = true;
        public bool InjuriesEnabled = true;
        public bool FatigueEnabled = true;
        public int MaxSubstitutions = 5;
        /// <summary>Multiplier on the computed stoppage time (0 disables added time).</summary>
        public float StoppageTimeScale = 1f;
        /// <summary>0..1, how much the engine corrects user pass direction/power.</summary>
        public float PassAssist = 0.7f;
        /// <summary>0..1, how much the engine corrects user shot aim.</summary>
        public float ShotAssist = 0.5f;
        /// <summary>Simulation speed multiplier (gameplay speed, not clock rate). 0.85..1.15.</summary>
        public float GameSpeed = 1f;
        public ControlMode ControlMode = ControlMode.TeamSwitching;
        public ulong Seed = 12345;

        public float RealSecondsPerHalf => MathUtil.Clamp(RealMinutesPerHalf, 1, 45) * 60f;

        public void Validate()
        {
            if (Array.IndexOf(HalfLengthOptions, RealMinutesPerHalf) < 0)
            {
                int best = HalfLengthOptions[0];
                foreach (var o in HalfLengthOptions) if (Math.Abs(o - RealMinutesPerHalf) < Math.Abs(best - RealMinutesPerHalf)) best = o;
                RealMinutesPerHalf = best;
            }
            PassAssist = MathUtil.Clamp01(PassAssist);
            ShotAssist = MathUtil.Clamp01(ShotAssist);
            GameSpeed = MathUtil.Clamp(GameSpeed, 0.85f, 1.15f);
            MaxSubstitutions = MathUtil.Clamp(MaxSubstitutions, 0, 5);
            StoppageTimeScale = MathUtil.Clamp(StoppageTimeScale, 0f, 2f);
        }

        /// <summary>Decision noise and reaction delay for AI given the difficulty (applied to the user's opponents).</summary>
        public float OpponentDecisionNoise
        {
            get
            {
                switch (Difficulty)
                {
                    case Difficulty.Amateur: return 0.55f;
                    case Difficulty.Semipro: return 0.38f;
                    case Difficulty.Professional: return 0.24f;
                    case Difficulty.WorldClass: return 0.14f;
                    default: return 0.07f;
                }
            }
        }

        public float OpponentReactionDelay
        {
            get
            {
                switch (Difficulty)
                {
                    case Difficulty.Amateur: return 0.42f;
                    case Difficulty.Semipro: return 0.33f;
                    case Difficulty.Professional: return 0.25f;
                    case Difficulty.WorldClass: return 0.19f;
                    default: return 0.14f;
                }
            }
        }

        /// <summary>Execution quality multiplier (kick error scale) for opponents. Speeds are never altered.</summary>
        public float OpponentExecutionError
        {
            get
            {
                switch (Difficulty)
                {
                    case Difficulty.Amateur: return 1.6f;
                    case Difficulty.Semipro: return 1.3f;
                    case Difficulty.Professional: return 1.0f;
                    case Difficulty.WorldClass: return 0.85f;
                    default: return 0.72f;
                }
            }
        }

        public MatchSettings Clone() => (MatchSettings)MemberwiseClone();
    }
}
