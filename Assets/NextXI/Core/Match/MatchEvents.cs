using System;
using System.Collections.Generic;

namespace NextXI.Core.Match
{
    public enum MatchEventType
    {
        KickOff,
        Whistle,
        Pass,
        PassCompleted,
        Shot,
        ShotOnTarget,
        Goal,
        Save,
        Woodwork,
        Tackle,
        TackleWon,
        Interception,
        Foul,
        YellowCard,
        RedCard,
        Injury,
        Offside,
        Corner,
        ThrowIn,
        GoalKick,
        FreeKick,
        Penalty,
        Substitution,
        HalfTime,
        SecondHalf,
        FullTime,
        StoppageAnnounced,
        BallTouch,
        BallBounce,
        Dribble,
        Cross,
        Header,
        KeeperClaim,
        ChanceMissed
    }

    [Serializable]
    public struct MatchEvent
    {
        public MatchEventType Type;
        /// <summary>Virtual seconds since kick-off.</summary>
        public float VirtualTime;
        public int Minute;
        public int Team;
        public int PlayerId;
        public int OtherPlayerId;
        public Vec3 Position;
        /// <summary>Generic magnitude: ball speed for kicks, xG for shots, minutes for stoppage.</summary>
        public float Value;

        public override string ToString() => $"{Minute}' {Type} T{Team} P{PlayerId} {Value:0.00}";
    }

    [Serializable]
    public sealed class TeamMatchStats
    {
        public int Goals, Shots, ShotsOnTarget, Passes, PassesCompleted, Tackles, TacklesWon, Interceptions;
        public int Fouls, YellowCards, RedCards, Corners, Offsides, Saves, Substitutions;
        public float PossessionSeconds, XG;

        public float PassAccuracy => Passes == 0 ? 0 : (float)PassesCompleted / Passes;
    }

    /// <summary>Holds the events of a match and a per-step queue consumed by presentation layers.</summary>
    public sealed class MatchEventLog
    {
        public readonly List<MatchEvent> All = new List<MatchEvent>();
        private readonly List<MatchEvent> _pending = new List<MatchEvent>();

        /// <summary>Raised synchronously for every event (audio, camera, commentary hooks).</summary>
        public event Action<MatchEvent> OnEvent;

        /// <summary>High-frequency events (touches, bounces) are not stored in All to bound memory.</summary>
        public static bool IsTransient(MatchEventType t) => t == MatchEventType.BallTouch || t == MatchEventType.BallBounce || t == MatchEventType.Dribble;

        public void Add(MatchEvent e)
        {
            if (!IsTransient(e.Type)) All.Add(e);
            _pending.Add(e);
            OnEvent?.Invoke(e);
        }

        /// <summary>Returns and clears the events produced since the last call.</summary>
        public void Drain(List<MatchEvent> into)
        {
            into.AddRange(_pending);
            _pending.Clear();
        }

        public void ClearPending() => _pending.Clear();

        public int Count(MatchEventType type, int team = -1)
        {
            int n = 0;
            foreach (var e in All) if (e.Type == type && (team < 0 || e.Team == team)) n++;
            return n;
        }
    }
}
