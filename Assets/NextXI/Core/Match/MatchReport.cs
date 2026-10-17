using System;
using System.Collections.Generic;

namespace NextXI.Core.Match
{
    [Serializable]
    public sealed class GoalRecord
    {
        public int Minute;
        public int Team;
        public string ScorerId;
        public string AssistId;
        public bool OwnGoal;
        public bool Penalty;
    }

    [Serializable]
    public sealed class PlayerMatchLine
    {
        public string ProfileId;
        public int Team;
        public bool Started;
        public int Minutes;
        public float Rating;
        public int Goals, Assists, Shots, ShotsOnTarget, Passes, PassesCompleted, KeyPasses, Tackles, TacklesWon, Interceptions, Saves, YellowCards, RedCards, Fouls;
        public float DistanceKm;
        public bool Injured;
    }

    /// <summary>Engine-independent summary of a finished match (used by career, news, stats).</summary>
    [Serializable]
    public sealed class MatchReport
    {
        public string HomeClubId, AwayClubId;
        public int HomeGoals, AwayGoals;
        public TeamMatchStats HomeStats = new TeamMatchStats(), AwayStats = new TeamMatchStats();
        public List<GoalRecord> Goals = new List<GoalRecord>();
        public List<PlayerMatchLine> Players = new List<PlayerMatchLine>();
        /// <summary>"3d" for the played match, "detailed"/"light" for simulated ones.</summary>
        public string SimulationLevel = "3d";
        public string ManOfTheMatchId;

        public PlayerMatchLine Line(string profileId) => Players.Find(p => p.ProfileId == profileId);

        public static MatchReport FromEngine(MatchEngine m)
        {
            if (!m.IsFinished) m.FinalizeRatings();
            var r = new MatchReport
            {
                HomeClubId = m.Teams[0].Sheet.ClubId,
                AwayClubId = m.Teams[1].Sheet.ClubId,
                HomeGoals = m.Score(0),
                AwayGoals = m.Score(1),
                HomeStats = m.Teams[0].Stats,
                AwayStats = m.Teams[1].Stats
            };
            int lastPenaltyTeam = -1;
            foreach (var e in m.Events.All)
            {
                if (e.Type == MatchEventType.Penalty) lastPenaltyTeam = e.Team;
                if (e.Type != MatchEventType.Goal) continue;
                var scorer = m.FindPlayerAny(e.PlayerId);
                var assist = m.FindPlayerAny(e.OtherPlayerId);
                r.Goals.Add(new GoalRecord
                {
                    Minute = e.Minute, Team = e.Team, ScorerId = scorer?.Profile.Id, AssistId = assist?.Profile.Id,
                    OwnGoal = e.Value > 0.5f, Penalty = lastPenaltyTeam == e.Team
                });
                lastPenaltyTeam = -1;
            }
            float best = -1f;
            for (int t = 0; t < 2; t++)
            {
                var team = m.Teams[t];
                var all = new List<MatchPlayer>(team.Players);
                all.AddRange(team.SubbedOff);
                foreach (var p in all)
                {
                    if (p.Stats.VirtualMinutesPlayed < 0.5f) continue;
                    var s = p.Stats;
                    var line = new PlayerMatchLine
                    {
                        ProfileId = p.Profile.Id, Team = t, Started = p.Starter, Minutes = (int)Math.Round(Math.Min(s.VirtualMinutesPlayed, 90f)),
                        Rating = (float)Math.Round(s.Rating, 1), Goals = s.Goals, Assists = s.Assists, Shots = s.Shots, ShotsOnTarget = s.ShotsOnTarget,
                        Passes = s.Passes, PassesCompleted = s.PassesCompleted, KeyPasses = s.KeyPasses, Tackles = s.Tackles, TacklesWon = s.TacklesWon,
                        Interceptions = s.Interceptions, Saves = s.Saves, YellowCards = s.YellowCards, RedCards = s.RedCards, Fouls = s.Fouls,
                        DistanceKm = s.DistanceMeters / 1000f, Injured = p.Injured
                    };
                    r.Players.Add(line);
                    if (line.Rating > best) { best = line.Rating; r.ManOfTheMatchId = line.ProfileId; }
                }
            }
            return r;
        }
    }
}
