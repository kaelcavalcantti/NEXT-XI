using System;

namespace NextXI.Core.Match
{
    public enum MatchPeriod { PreMatch, FirstHalf, HalfTime, SecondHalf, FullTime }

    /// <summary>
    /// Maps real simulated seconds to virtual match time. A half always lasts 45 virtual
    /// minutes (plus stoppage) regardless of the chosen real duration; only the clock rate
    /// changes, so events, statistics and fatigue scale predictably.
    /// </summary>
    [Serializable]
    public sealed class MatchClock
    {
        public const float VirtualHalfSeconds = 45f * 60f;

        public MatchPeriod Period { get; private set; } = MatchPeriod.PreMatch;
        /// <summary>Virtual seconds elapsed inside the current half (0..2700+stoppage).</summary>
        public float HalfVirtualSeconds { get; private set; }
        /// <summary>Stoppage minutes announced for the current half (set at the 45' mark).</summary>
        public int StoppageMinutes { get; private set; }
        public bool StoppageAnnounced { get; private set; }
        /// <summary>Accumulated stoppage seconds requested by events in the current half.</summary>
        public float PendingStoppageSeconds { get; private set; }

        public float Rate { get; }
        public float RealSecondsPerHalf { get; }
        private readonly float _stoppageScale;

        public MatchClock(float realSecondsPerHalf, float stoppageScale = 1f)
        {
            RealSecondsPerHalf = MathF.Max(10f, realSecondsPerHalf);
            Rate = VirtualHalfSeconds / RealSecondsPerHalf;
            _stoppageScale = stoppageScale;
        }

        /// <summary>Virtual seconds since kick-off (second half starts at 45:00).</summary>
        public float TotalVirtualSeconds =>
            Period == MatchPeriod.SecondHalf || Period == MatchPeriod.FullTime
                ? VirtualHalfSeconds + HalfVirtualSeconds
                : HalfVirtualSeconds;

        public int DisplayMinute
        {
            get
            {
                float baseSec = Period >= MatchPeriod.SecondHalf ? VirtualHalfSeconds : 0f;
                return (int)((baseSec + MathF.Min(HalfVirtualSeconds, VirtualHalfSeconds)) / 60f);
            }
        }

        /// <summary>Formatted clock, e.g. "37:12" or "45+2".</summary>
        public string Display
        {
            get
            {
                float baseSec = Period >= MatchPeriod.SecondHalf ? VirtualHalfSeconds : 0f;
                if (HalfVirtualSeconds >= VirtualHalfSeconds)
                {
                    int extra = (int)((HalfVirtualSeconds - VirtualHalfSeconds) / 60f) + 1;
                    return $"{(int)((baseSec + VirtualHalfSeconds) / 60f)}+{extra}";
                }
                float t = baseSec + HalfVirtualSeconds;
                return $"{(int)(t / 60f):00}:{(int)(t % 60f):00}";
            }
        }

        public bool IsRunning => Period == MatchPeriod.FirstHalf || Period == MatchPeriod.SecondHalf;

        public void StartHalf(MatchPeriod period)
        {
            if (period != MatchPeriod.FirstHalf && period != MatchPeriod.SecondHalf)
                throw new ArgumentException("Only halves can be started");
            Period = period;
            HalfVirtualSeconds = 0;
            StoppageMinutes = 0;
            StoppageAnnounced = false;
            PendingStoppageSeconds = 0;
        }

        public void AddStoppage(float virtualSeconds) => PendingStoppageSeconds += MathF.Max(0, virtualSeconds);

        /// <summary>Advance by real seconds. Returns true when the half has just ended.</summary>
        public bool Tick(float realDt)
        {
            if (!IsRunning) return false;
            HalfVirtualSeconds += realDt * Rate;
            if (!StoppageAnnounced && HalfVirtualSeconds >= VirtualHalfSeconds)
            {
                StoppageAnnounced = true;
                // Base 1 minute, plus requested seconds, capped to 6 minutes.
                float minutes = (60f + PendingStoppageSeconds) / 60f * _stoppageScale;
                StoppageMinutes = _stoppageScale <= 0f ? 0 : MathUtil.Clamp((int)MathF.Round(minutes), 1, 6);
            }
            return StoppageAnnounced && HalfVirtualSeconds >= VirtualHalfSeconds + StoppageMinutes * 60f;
        }

        /// <summary>The half is in stoppage and the referee may end it at the next safe moment.</summary>
        public bool HalfExpired => StoppageAnnounced && HalfVirtualSeconds >= VirtualHalfSeconds + StoppageMinutes * 60f;

        public void EnterHalfTime() { Period = MatchPeriod.HalfTime; }
        public void EnterFullTime() { Period = MatchPeriod.FullTime; }

        /// <summary>Converts a real-time duration into virtual seconds (for fatigue, distances).</summary>
        public float ToVirtual(float realSeconds) => realSeconds * Rate;
    }
}
