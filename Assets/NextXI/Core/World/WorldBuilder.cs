using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NextXI.Core.Data;
using NextXI.Core.Match;
using NextXI.Core.Persistence;

namespace NextXI.Core.World
{
    /// <summary>Round-robin and knockout scheduling.</summary>
    public static class Scheduler
    {
        /// <summary>
        /// Double round robin (circle method). Every club plays every other twice, once at home.
        /// Rounds are placed every `daysBetween` days starting at `firstDay`.
        /// </summary>
        public static List<Fixture> DoubleRoundRobin(string compId, IList<string> clubs, int firstDay, int daysBetween, Rng rng)
        {
            var teams = new List<string>(clubs);
            // Shuffle so the calendar differs between seasons.
            for (int i = teams.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); (teams[i], teams[j]) = (teams[j], teams[i]); }
            if (teams.Count % 2 == 1) teams.Add(null); // bye
            int n = teams.Count;
            var fixtures = new List<Fixture>();
            int rounds = n - 1;
            for (int leg = 0; leg < 2; leg++)
            {
                var rot = new List<string>(teams);
                for (int r = 0; r < rounds; r++)
                {
                    int roundNo = leg * rounds + r + 1;
                    for (int i = 0; i < n / 2; i++)
                    {
                        string a = rot[i], b = rot[n - 1 - i];
                        if (a == null || b == null) continue;
                        bool swap = (r + i) % 2 == 1;
                        if (leg == 1) swap = !swap;
                        fixtures.Add(new Fixture
                        {
                            Id = $"{compId}-r{roundNo}-{i}",
                            CompetitionId = compId,
                            Round = roundNo,
                            Day = firstDay + (roundNo - 1) * daysBetween,
                            HomeId = swap ? b : a,
                            AwayId = swap ? a : b
                        });
                    }
                    // Rotate all but the first.
                    var last = rot[n - 1];
                    rot.RemoveAt(n - 1);
                    rot.Insert(1, last);
                }
            }
            return fixtures;
        }

        /// <summary>Single-leg knockout round from a list of qualified clubs (seeded by order).</summary>
        public static List<Fixture> KnockoutRound(string compId, int round, IList<string> clubs, int day)
        {
            var list = new List<Fixture>();
            int n = clubs.Count;
            for (int i = 0; i < n / 2; i++)
                list.Add(new Fixture { Id = $"{compId}-k{round}-{i}", CompetitionId = compId, Round = round, Day = day, HomeId = clubs[i], AwayId = clubs[n - 1 - i] });
            return list;
        }
    }

    /// <summary>JSON data pack format for importing countries/leagues/clubs/players without code changes.</summary>
    [Serializable]
    public sealed class WorldPack
    {
        public string PackId;
        public string Description;
        /// <summary>Where the data comes from, licence and last update (mandatory for real data).</summary>
        public string Source;
        public string License;
        public string UpdatedAt;
        public bool Fictional = true;
        public List<ClubRecord> Clubs = new List<ClubRecord>();
        public List<PlayerProfile> Players = new List<PlayerProfile>();
        public List<CoachRecord> Coaches = new List<CoachRecord>();
        public List<LeagueDef> Leagues = new List<LeagueDef>();
    }

    [Serializable]
    public sealed class LeagueDef
    {
        public string Id, Name, Country;
        public int Tier = 1;
        public List<string> ClubIds = new List<string>();
    }

    /// <summary>Builds the demo world and imports data packs.</summary>
    public static class WorldBuilder
    {
        public const string BrazilLeague = "liga-nacional";
        public const string EuropeLeague = "liga-continental";
        public const string NationalCup = "copa-nacional";

        public static WorldState CreateDemoWorld(int season, ulong seed)
        {
            var w = new WorldState { Season = season, SeasonStartDate = $"{season}-02-01" };
            w.DataSources.Add("NEXT XI demo universe — all clubs, players and coaches are fictional (generated).");
            var rng = new Rng(seed);
            AddDemoLeague(w, BrazilLeague, "Liga Nacional (demo)", "Brasil (fictício)", DemoClubs.Brazil, season, rng);
            AddDemoLeague(w, EuropeLeague, "Liga Continental (demo)", "Europa (fictício)", DemoClubs.Europe, season, rng);
            ScheduleSeason(w, rng);
            foreach (var p in w.Players.Values) UpdateMarketValue(w, p);
            return w;
        }

        private static void AddDemoLeague(WorldState w, string leagueId, string name, string country, IReadOnlyList<DemoClubs.Def> defs, int season, Rng rng)
        {
            var comp = new Competition { Id = leagueId, Name = name, Country = country, Format = CompetitionFormat.League };
            foreach (var d in defs)
            {
                var club = new ClubRecord
                {
                    Id = d.Id, Name = d.Name, ShortName = d.Short, City = d.City, Country = d.Country, LeagueId = leagueId,
                    Stadium = d.Stadium, Capacity = 18000 + (d.Strength - 60) * 2500 + rng.Range(0, 8000),
                    Reputation = MathUtil.Clamp(d.Strength - 10 + rng.Range(-3, 4), 30, 95),
                    Budget = (long)(Math.Pow(1.11, d.Strength - 60) * 2500),
                    WageBudget = (long)(Math.Pow(1.09, d.Strength - 60) * 120),
                    Formation = d.Formation, HomeKit = DemoClubs.Kit(d), AwayKit = DemoClubs.AwayKit(d),
                    Tactics = new TeamTactics { LineHeight = rng.Range(0.35f, 0.7f), Pressing = rng.Range(0.35f, 0.75f), Width = rng.Range(0.4f, 0.7f), Directness = rng.Range(0.3f, 0.65f) }
                };
                var coach = new CoachRecord
                {
                    Id = "coach-" + d.Id, Name = PlayerGenerator.RandomName(rng, out _, out _), Nationality = d.Country,
                    Style = rng.Next(), YouthTrust = rng.Range(0.2f, 0.85f), Reputation = MathUtil.Clamp(d.Strength - 15 + rng.Range(-5, 6), 20, 95), ClubId = d.Id
                };
                club.CoachId = coach.Id;
                w.Coaches[coach.Id] = coach;
                foreach (var p in DemoClubs.Squad(d, season))
                {
                    w.Players[p.Id] = p;
                    club.Squad.Add(p.Id);
                    var st = w.State(p.Id);
                    int ovr = p.Overall;
                    st.Contract = new Contract
                    {
                        ClubId = d.Id, Wage = WageFor(ovr, p.AgeOn(SeasonStart(w))), UntilSeason = season + rng.Range(0, 4),
                        Role = ovr >= d.Strength + 3 ? SquadRole.KeyPlayer : ovr >= d.Strength - 2 ? SquadRole.Starter : ovr >= d.Strength - 7 ? SquadRole.Rotation : SquadRole.Prospect
                    };
                }
                w.Clubs[club.Id] = club;
                comp.ClubIds.Add(club.Id);
            }
            w.Competitions.Add(comp);
        }

        public static DateTime SeasonStart(WorldState w) =>
            DateTime.TryParse(w.SeasonStartDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d : new DateTime(w.Season, 2, 1);

        public static int WageFor(int overall, int age) => Math.Max(1, (int)(Math.Pow(1.115, overall - 55) * 2.2 * (age < 21 ? 0.6 : 1.0)));

        /// <summary>(Re)creates fixtures for every competition of the current season.</summary>
        public static void ScheduleSeason(WorldState w, Rng rng)
        {
            foreach (var c in w.Competitions.Where(c => c.Format == CompetitionFormat.League))
            {
                c.Fixtures = Scheduler.DoubleRoundRobin(c.Id, c.ClubIds, 7, 7, rng);
                c.ChampionId = null;
            }
            // National cup: top 8 of the domestic league by strength, single-leg knockout on Wednesdays.
            var cup = w.Competition(NationalCup);
            if (cup == null)
            {
                cup = new Competition { Id = NationalCup, Name = "Copa Nacional (demo)", Country = "Brasil (fictício)", Format = CompetitionFormat.Knockout };
                w.Competitions.Add(cup);
            }
            var league = w.Competition(BrazilLeague);
            if (league != null)
            {
                cup.ClubIds = league.ClubIds.OrderByDescending(id => w.Strength(id) + rng.Range(-4f, 4f)).Take(8).ToList();
                cup.Fixtures = Scheduler.KnockoutRound(cup.Id, 1, cup.ClubIds, 24);
                cup.ChampionId = null;
            }
        }

        /// <summary>Creates the next knockout round when the current one is complete.</summary>
        public static bool AdvanceKnockout(Competition cup)
        {
            if (cup.Format != CompetitionFormat.Knockout || cup.ChampionId != null) return false;
            int round = cup.Fixtures.Max(f => f.Round);
            var current = cup.Fixtures.Where(f => f.Round == round).ToList();
            if (!current.All(f => f.Played)) return false;
            var winners = current.Select(f => f.WinnerId).ToList();
            if (winners.Count == 1) { cup.ChampionId = winners[0]; return true; }
            int day = current.Max(f => f.Day) + 28;
            cup.Fixtures.AddRange(Scheduler.KnockoutRound(cup.Id, round + 1, winners, day));
            return true;
        }

        /// <summary>Market value (thousands) from overall, age, potential, form, contract length.</summary>
        public static void UpdateMarketValue(WorldState w, PlayerProfile p)
        {
            var st = w.State(p.Id);
            int age = p.AgeOn(SeasonStart(w));
            double baseV = Math.Pow(1.17, p.Overall - 50) * 60;
            double ageF = age <= 21 ? 1.25 + (p.Potential - p.Overall) * 0.03 : age <= 27 ? 1.1 : age <= 30 ? 0.8 : age <= 33 ? 0.45 : 0.2;
            double formF = 0.85 + (st.Form - 6.0) * 0.12;
            int years = Math.Max(0, st.Contract.UntilSeason - w.Season);
            double contractF = years == 0 ? 0.55 : years == 1 ? 0.8 : 1.0;
            st.MarketValue = (long)Math.Max(10, baseV * ageF * Math.Max(0.6, formF) * contractF);
        }

        // ------------------------------------------------------------------ import

        public static WorldPack ParsePack(string json) => JsonConvert.DeserializeObject<WorldPack>(json, SaveSystem.JsonSettings);

        /// <summary>
        /// Imports a data pack: adds/updates clubs, players, coaches and leagues. Validates ids and
        /// references; returns a list of problems (empty = ok). Real-data packs must declare Source,
        /// License and UpdatedAt or they are rejected.
        /// </summary>
        public static List<string> Import(WorldState w, WorldPack pack)
        {
            var errors = new List<string>();
            if (pack == null) { errors.Add("pack is null"); return errors; }
            if (!pack.Fictional && (string.IsNullOrWhiteSpace(pack.Source) || string.IsNullOrWhiteSpace(pack.License) || string.IsNullOrWhiteSpace(pack.UpdatedAt)))
            {
                errors.Add("Real-data packs must declare Source, License and UpdatedAt.");
                return errors;
            }
            foreach (var c in pack.Clubs ?? new List<ClubRecord>())
            {
                if (string.IsNullOrWhiteSpace(c.Id) || string.IsNullOrWhiteSpace(c.Name)) { errors.Add("club without id/name"); continue; }
                c.IsFictional = pack.Fictional;
                c.DataSource = pack.Source ?? c.DataSource;
                if (c.Squad == null) c.Squad = new List<string>();
                w.Clubs[c.Id] = c;
            }
            foreach (var p in pack.Players ?? new List<PlayerProfile>())
            {
                if (string.IsNullOrWhiteSpace(p.Id)) { errors.Add("player without id"); continue; }
                if (p.Attributes == null) { errors.Add($"player {p.Id} without attributes"); continue; }
                p.IsFictional = pack.Fictional;
                p.DataSource = pack.Source ?? p.DataSource;
                w.Players[p.Id] = p;
                if (p.ClubId != null)
                {
                    var club = w.Club(p.ClubId);
                    if (club == null) errors.Add($"player {p.Id} references unknown club {p.ClubId}");
                    else if (!club.Squad.Contains(p.Id)) club.Squad.Add(p.Id);
                    var st = w.State(p.Id);
                    st.Contract.ClubId = p.ClubId;
                    if (st.Contract.Wage <= 0) st.Contract.Wage = WageFor(p.Overall, p.AgeOn(SeasonStart(w)));
                }
            }
            foreach (var co in pack.Coaches ?? new List<CoachRecord>())
            {
                if (string.IsNullOrWhiteSpace(co.Id)) { errors.Add("coach without id"); continue; }
                w.Coaches[co.Id] = co;
                var club = w.Club(co.ClubId);
                if (club != null) club.CoachId = co.Id;
            }
            foreach (var l in pack.Leagues ?? new List<LeagueDef>())
            {
                if (l.ClubIds.Any(id => w.Club(id) == null)) { errors.Add($"league {l.Id} references unknown clubs"); continue; }
                var comp = w.Competition(l.Id) ?? new Competition { Id = l.Id };
                comp.Name = l.Name; comp.Country = l.Country; comp.Tier = l.Tier; comp.Format = CompetitionFormat.League;
                comp.ClubIds = new List<string>(l.ClubIds);
                if (!w.Competitions.Contains(comp)) w.Competitions.Add(comp);
                foreach (var id in l.ClubIds) w.Club(id).LeagueId = l.Id;
            }
            if (!string.IsNullOrWhiteSpace(pack.Source))
                w.DataSources.Add($"{pack.PackId}: {pack.Source} ({pack.License}, {pack.UpdatedAt}){(pack.Fictional ? " [fictional]" : "")}");
            return errors;
        }
    }
}
