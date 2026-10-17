using System;
using System.Collections.Generic;
using NextXI.Core.Data;

namespace NextXI.Core.Match
{
    [Serializable]
    public sealed class TeamTactics
    {
        /// <summary>0 = low block, 1 = very high line.</summary>
        public float LineHeight = 0.5f;
        /// <summary>0 = passive, 1 = aggressive pressing.</summary>
        public float Pressing = 0.5f;
        /// <summary>0 = narrow, 1 = very wide in possession.</summary>
        public float Width = 0.55f;
        /// <summary>0 = patient build-up, 1 = direct/vertical.</summary>
        public float Directness = 0.45f;

        public TeamTactics Clone() => (TeamTactics)MemberwiseClone();
    }

    [Serializable]
    public sealed class KitColors
    {
        public string Shirt = "#C8102E";
        public string Shorts = "#FFFFFF";
        public string Socks = "#C8102E";
        public string Number = "#FFFFFF";
        public string Keeper = "#2E7D32";
    }

    /// <summary>Input to a match: who plays, in which formation, with which kit.</summary>
    [Serializable]
    public sealed class TeamSheet
    {
        public string ClubId;
        public string Name;
        public string ShortName;
        public string FormationName = "4-3-3";
        public KitColors Kit = new KitColors();
        public TeamTactics Tactics = new TeamTactics();
        /// <summary>Exactly 11 players ordered to match the formation slots (index 0 = GK).</summary>
        public List<PlayerProfile> StartingXI = new List<PlayerProfile>();
        public List<PlayerProfile> Bench = new List<PlayerProfile>();
        public bool AiControlledCoach = true;
    }

    public sealed class MatchTeam
    {
        public readonly int Index;
        public readonly TeamSheet Sheet;
        public readonly Formation Formation;
        public readonly List<MatchPlayer> Players = new List<MatchPlayer>();
        public readonly List<MatchPlayer> Bench = new List<MatchPlayer>();
        public readonly List<MatchPlayer> SubbedOff = new List<MatchPlayer>();
        public readonly TeamMatchStats Stats = new TeamMatchStats();
        public int SubstitutionsUsed;
        public TeamTactics Tactics => Sheet.Tactics;

        public MatchTeam(int index, TeamSheet sheet)
        {
            Index = index;
            Sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
            Formation = Formation.Get(sheet.FormationName);
        }

        public string Name => Sheet.Name;
        public string ShortName => string.IsNullOrEmpty(Sheet.ShortName) ? (Sheet.Name ?? "TEAM").ToUpperInvariant().Substring(0, Math.Min(3, (Sheet.Name ?? "TEAM").Length)) : Sheet.ShortName;

        public IEnumerable<MatchPlayer> Active
        {
            get { foreach (var p in Players) if (p.IsActive) yield return p; }
        }

        public MatchPlayer Goalkeeper
        {
            get
            {
                foreach (var p in Players) if (p.IsActive && p.IsGoalkeeper) return p;
                return null;
            }
        }

        public int ActiveCount
        {
            get { int n = 0; foreach (var p in Players) if (p.IsActive) n++; return n; }
        }
    }
}
