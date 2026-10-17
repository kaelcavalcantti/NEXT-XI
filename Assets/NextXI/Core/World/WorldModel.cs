using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core.Data;
using NextXI.Core.Match;

namespace NextXI.Core.World
{
    [Serializable]
    public sealed class CoachRecord
    {
        public string Id;
        public string Name;
        public string Nationality;
        /// <summary>0 = defensive/pragmatic, 1 = attacking/possession.</summary>
        public float Style = 0.5f;
        /// <summary>How much the coach trusts youth (0..1).</summary>
        public float YouthTrust = 0.5f;
        public int Reputation = 50;
        public string ClubId;
        public bool IsFictional = true;
    }

    [Serializable]
    public sealed class ClubRecord
    {
        public string Id;
        public string Name;
        public string ShortName;
        public string City;
        public string Country;
        public string LeagueId;
        public string Stadium;
        public int Capacity = 30000;
        public int Reputation = 60;
        /// <summary>Transfer budget in thousands (fictional currency units).</summary>
        public long Budget = 10000;
        /// <summary>Weekly wage budget in thousands.</summary>
        public long WageBudget = 400;
        public string Formation = "4-3-3";
        public KitColors HomeKit = new KitColors();
        public KitColors AwayKit = new KitColors();
        public TeamTactics Tactics = new TeamTactics();
        public string CoachId;
        public List<string> Squad = new List<string>();
        public string DataSource = "NEXT XI demo universe (fictional)";
        public bool IsFictional = true;
    }

    [Serializable]
    public sealed class Contract
    {
        public string ClubId;
        /// <summary>Weekly wage in thousands.</summary>
        public int Wage = 5;
        /// <summary>Last season year covered.</summary>
        public int UntilSeason = 2027;
        public SquadRole Role = SquadRole.Rotation;
        public long ReleaseClause;
        /// <summary>When on loan, the parent club.</summary>
        public string LoanFromClubId;
        public int LoanUntilSeason;
    }

    public enum SquadRole { Prospect, Rotation, Starter, KeyPlayer }

    [Serializable]
    public sealed class SeasonStatLine
    {
        public int Season;
        public string ClubId;
        public string CompetitionId;
        public int Apps, Starts, Minutes, Goals, Assists, YellowCards, RedCards, CleanSheets, MotM;
        public float RatingSum;
        public float AvgRating => Apps == 0 ? 0 : RatingSum / Apps;
    }

    /// <summary>Mutable per-career state of a player (contract, form, condition, history).</summary>
    [Serializable]
    public sealed class PlayerState
    {
        public string PlayerId;
        public Contract Contract = new Contract();
        public float Form = 6.5f;        // exponential average of ratings
        public float Morale = 0.6f;      // 0..1
        public int InjuryDays;
        public string InjuryName;
        public int SuspensionMatches;
        public int YellowAccumulation;
        public long MarketValue;         // thousands
        public List<SeasonStatLine> Stats = new List<SeasonStatLine>();
        public List<string> TransferHistory = new List<string>();
        public bool Retired;

        public SeasonStatLine Line(int season, string clubId, string comp)
        {
            var l = Stats.Find(s => s.Season == season && s.ClubId == clubId && s.CompetitionId == comp);
            if (l == null) { l = new SeasonStatLine { Season = season, ClubId = clubId, CompetitionId = comp }; Stats.Add(l); }
            return l;
        }
    }

    public enum CompetitionFormat { League, Knockout }

    [Serializable]
    public sealed class Fixture
    {
        public string Id;
        public string CompetitionId;
        public int Round;
        /// <summary>Day number since the season start.</summary>
        public int Day;
        public string HomeId, AwayId;
        public bool Played;
        public int HomeGoals, AwayGoals;
        /// <summary>Penalties winner for knockout draws ("" otherwise).</summary>
        public string PenaltyWinnerId = "";
        public string SimLevel = "";
        public List<GoalRecord> Goals = new List<GoalRecord>();

        public string WinnerId => !Played ? null : HomeGoals > AwayGoals ? HomeId : AwayGoals > HomeGoals ? AwayId : (string.IsNullOrEmpty(PenaltyWinnerId) ? null : PenaltyWinnerId);
        public bool Involves(string clubId) => HomeId == clubId || AwayId == clubId;
    }

    [Serializable]
    public sealed class TableRow
    {
        public string ClubId;
        public int Played, Won, Drawn, Lost, GoalsFor, GoalsAgainst;
        public int Points => Won * 3 + Drawn;
        public int GoalDiff => GoalsFor - GoalsAgainst;
        public List<char> Last5 = new List<char>();
    }

    [Serializable]
    public sealed class Competition
    {
        public string Id;
        public string Name;
        public string Country;
        public CompetitionFormat Format;
        public int Tier = 1;
        /// <summary>"3d-detailed" competitions are simulated with the detailed model; others light.</summary>
        public List<string> ClubIds = new List<string>();
        public List<Fixture> Fixtures = new List<Fixture>();
        public string ChampionId;

        public List<TableRow> Table()
        {
            var rows = ClubIds.ToDictionary(c => c, c => new TableRow { ClubId = c });
            foreach (var f in Fixtures.Where(f => f.Played).OrderBy(f => f.Day))
            {
                if (!rows.TryGetValue(f.HomeId, out var h) || !rows.TryGetValue(f.AwayId, out var a)) continue;
                h.Played++; a.Played++;
                h.GoalsFor += f.HomeGoals; h.GoalsAgainst += f.AwayGoals;
                a.GoalsFor += f.AwayGoals; a.GoalsAgainst += f.HomeGoals;
                if (f.HomeGoals > f.AwayGoals) { h.Won++; a.Lost++; Push(h, 'V'); Push(a, 'D'); }
                else if (f.HomeGoals < f.AwayGoals) { a.Won++; h.Lost++; Push(a, 'V'); Push(h, 'D'); }
                else { h.Drawn++; a.Drawn++; Push(h, 'E'); Push(a, 'E'); }
            }
            return rows.Values
                .OrderByDescending(r => r.Points).ThenByDescending(r => r.Won).ThenByDescending(r => r.GoalDiff)
                .ThenByDescending(r => r.GoalsFor).ThenBy(r => r.ClubId, StringComparer.Ordinal).ToList();
        }

        private static void Push(TableRow r, char c)
        {
            r.Last5.Add(c);
            if (r.Last5.Count > 5) r.Last5.RemoveAt(0);
        }

        public int Position(string clubId)
        {
            var t = Table();
            return t.FindIndex(r => r.ClubId == clubId) + 1;
        }

        public bool Finished => Fixtures.Count > 0 && Fixtures.All(f => f.Played) && (Format == CompetitionFormat.League || ChampionId != null);
    }

    [Serializable]
    public sealed class TransferRecord
    {
        public int Season;
        public int Day;
        public string PlayerId;
        public string FromClubId, ToClubId;
        public long Fee;
        public bool Loan;
    }

    /// <summary>
    /// The football world of one career: static catalogue (clubs, players, coaches, leagues)
    /// plus mutable state (squads, contracts, fixtures, transfers). Data is importable from JSON
    /// packs (WorldImporter) so new countries/leagues/players need no code changes.
    /// </summary>
    [Serializable]
    public sealed class WorldState
    {
        public int Season = 2026;
        public string SeasonStartDate = "2026-02-01";
        public Dictionary<string, ClubRecord> Clubs = new Dictionary<string, ClubRecord>();
        public Dictionary<string, PlayerProfile> Players = new Dictionary<string, PlayerProfile>();
        public Dictionary<string, PlayerState> PlayerStates = new Dictionary<string, PlayerState>();
        public Dictionary<string, CoachRecord> Coaches = new Dictionary<string, CoachRecord>();
        public List<Competition> Competitions = new List<Competition>();
        public List<TransferRecord> Transfers = new List<TransferRecord>();
        public List<string> DataSources = new List<string>();
        public List<SeasonSummary> History = new List<SeasonSummary>();

        public ClubRecord Club(string id) => id != null && Clubs.TryGetValue(id, out var c) ? c : null;
        public PlayerProfile Player(string id) => id != null && Players.TryGetValue(id, out var p) ? p : null;
        public PlayerState State(string id)
        {
            if (id == null) return null;
            if (!PlayerStates.TryGetValue(id, out var s)) { s = new PlayerState { PlayerId = id }; PlayerStates[id] = s; }
            return s;
        }
        public CoachRecord Coach(string id) => id != null && Coaches.TryGetValue(id, out var c) ? c : null;
        public Competition Competition(string id) => Competitions.Find(c => c.Id == id);

        public IEnumerable<PlayerProfile> SquadOf(string clubId)
        {
            var c = Club(clubId);
            if (c == null) yield break;
            foreach (var id in c.Squad) { var p = Player(id); if (p != null) yield return p; }
        }

        /// <summary>Average overall of the best XI (team strength used by simulators and AI).</summary>
        public float Strength(string clubId)
        {
            var c = Club(clubId);
            if (c == null) return 50;
            var sq = SquadOf(clubId).Where(p => Available(p.Id)).ToList();
            if (sq.Count < 11) sq = SquadOf(clubId).ToList();
            if (sq.Count == 0) return 50;
            var sheet = TeamSheetBuilder.Build(c.Id, c.Name, c.ShortName, sq, c.Formation, c.HomeKit);
            return (float)sheet.StartingXI.Average(p => p.Attributes.Overall(p.Position));
        }

        public bool Available(string playerId)
        {
            var s = State(playerId);
            return s.InjuryDays <= 0 && s.SuspensionMatches <= 0 && !s.Retired;
        }

        public IEnumerable<PlayerProfile> Search(string text = null, string country = null, Position? pos = null, string clubId = null, string competitionId = null)
        {
            HashSet<string> compClubs = null;
            if (competitionId != null) compClubs = new HashSet<string>(Competition(competitionId)?.ClubIds ?? new List<string>());
            foreach (var p in Players.Values)
            {
                if (text != null && p.DisplayName.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (country != null && p.Nationality != country) continue;
                if (pos.HasValue && !p.CanPlay(pos.Value)) continue;
                if (clubId != null && p.ClubId != clubId) continue;
                if (compClubs != null && (p.ClubId == null || !compClubs.Contains(p.ClubId))) continue;
                yield return p;
            }
        }
    }

    [Serializable]
    public sealed class SeasonSummary
    {
        public int Season;
        public Dictionary<string, string> Champions = new Dictionary<string, string>();
        public string TopScorerId;
        public int TopScorerGoals;
        public string BestPlayerId;
    }
}
