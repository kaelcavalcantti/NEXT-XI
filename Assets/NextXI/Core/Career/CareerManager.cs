using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core.Data;
using NextXI.Core.Match;
using NextXI.Core.News;
using NextXI.Core.World;

namespace NextXI.Core.Career
{
    [Serializable]
    public sealed class CreationOptions
    {
        public string FirstName = "Novo";
        public string LastName = "Atleta";
        public string KnownAs;
        public string Nationality = "Brasil (fictício)";
        public Position Position = Position.ST;
        public Foot Foot = Foot.Right;
        public int HeightCm = 180;
        public int WeightKg = 76;
        public string ArchetypeId = "poacher";
        public CareerOrigin Origin = CareerOrigin.PromotedYouth;
        public string ClubId;
        public Appearance Appearance = new Appearance();
        public int ShirtNumber = 0;
        /// <summary>For CareerOrigin.ExistingAthlete: the catalogue player to control.</summary>
        public string ExistingPlayerId;
        public ulong Seed = 2026;
        public string SaveName;
    }

    /// <summary>What the coach decided for the next match of the athlete.</summary>
    public sealed class MatchPlan
    {
        public Fixture Fixture;
        public Competition Competition;
        public TeamSheet Home, Away;
        public int UserTeam;
        public bool Starter;
        public bool OnBench;
        public bool Unavailable;
        public string Reason;
    }

    /// <summary>
    /// Career rules: creation, calendar advance with layered world simulation, coach selection,
    /// applying match results (stats, XP, trust, form, objectives, news), training, coach
    /// conversations, transfer windows and offers, season end (awards, titles, contracts,
    /// ageing, retirements) and the athlete's own retirement.
    /// </summary>
    public static partial class CareerManager
    {
        public const int TransferWindow1End = 21, TransferWindow2Start = 98, TransferWindow2End = 119;
        public static readonly int[] InternationalWindows = { 63, 147 };

        public static Rng RngFor(CareerState s, int salt) => new Rng(s.Seed ^ (ulong)(s.World.Season * 1000003 + s.Day * 7919 + salt * 104729));

        // ================================================================== creation

        public static CareerState Create(CreationOptions o)
        {
            var world = WorldBuilder.CreateDemoWorld(2026, o.Seed);
            var s = new CareerState
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 12),
                SaveName = o.SaveName ?? $"{o.FirstName} {o.LastName}",
                CreatedUtc = DateTime.UtcNow.ToString("o"),
                Seed = o.Seed,
                World = world,
                ArchetypeId = o.ArchetypeId,
                Origin = o.Origin
            };
            var rng = new Rng(o.Seed * 31 + 7);
            string clubId = o.ClubId ?? DemoClubs.Brazil[0].Id;
            if (world.Club(clubId) == null) clubId = DemoClubs.Brazil[0].Id;
            PlayerProfile athlete;

            if (o.Origin == CareerOrigin.ExistingAthlete && o.ExistingPlayerId != null && world.Player(o.ExistingPlayerId) != null)
            {
                athlete = world.Player(o.ExistingPlayerId);
                clubId = athlete.ClubId;
                var arch = Archetype.ForPosition(athlete.Position).FirstOrDefault() ?? Archetype.All[0];
                s.ArchetypeId = arch.Id;
            }
            else
            {
                int age, baseOvr, potential;
                switch (o.Origin)
                {
                    case CareerOrigin.Academy: age = 17; baseOvr = 57; potential = 86; break;
                    case CareerOrigin.Professional: age = 22; baseOvr = 69; potential = 82; break;
                    default: age = 18; baseOvr = 62; potential = 84; break;
                }
                var arch = Archetype.Get(o.ArchetypeId);
                var attrs = PlayerGenerator.GenerateAttributes(rng, o.Position, baseOvr, o.HeightCm);
                foreach (var kv in arch.Bias) attrs.Set(kv.Key, attrs.Get(kv.Key) + kv.Value);
                // Re-centre on the target overall after the archetype bias.
                int diff = baseOvr - attrs.Overall(o.Position);
                foreach (var kv in PlayerAttributes.OverallWeights(o.Position)) attrs.Set(kv.Key, attrs.Get(kv.Key) + diff);
                attrs.WeakFoot = 3;
                o.Appearance.Clamp();
                athlete = new PlayerProfile
                {
                    Id = "athlete-" + s.Id,
                    FirstName = o.FirstName, LastName = o.LastName, KnownAs = string.IsNullOrWhiteSpace(o.KnownAs) ? null : o.KnownAs,
                    Nationality = o.Nationality,
                    BirthDate = $"{world.Season - age}-{rng.Range(1, 13):00}-{rng.Range(1, 28):00}",
                    HeightCm = MathUtil.Clamp(o.HeightCm, 160, 202), WeightKg = MathUtil.Clamp(o.WeightKg, 55, 105),
                    Position = o.Position, PreferredFoot = o.Foot, Attributes = attrs, Appearance = o.Appearance,
                    ClubId = clubId, IsFictional = true, DataSource = "Atleta criado pelo usuário", Potential = potential,
                    Reputation = o.Origin == CareerOrigin.Professional ? 35 : o.Origin == CareerOrigin.PromotedYouth ? 20 : 10
                };
                athlete.ShirtNumber = PickShirtNumber(world, clubId, o.ShirtNumber, o.Position);
                world.Players[athlete.Id] = athlete;
                var club = world.Club(clubId);
                // Keep the squad size: the weakest player of the same group leaves on a free.
                var group = PositionInfo.Group(o.Position);
                var cut = world.SquadOf(clubId).Where(p => PositionInfo.Group(p.Position) == group).OrderBy(p => p.Overall).FirstOrDefault();
                if (cut != null && club.Squad.Count >= 20) { club.Squad.Remove(cut.Id); cut.ClubId = null; world.State(cut.Id).Contract.ClubId = null; }
                club.Squad.Add(athlete.Id);
                var st = world.State(athlete.Id);
                st.Contract = new Contract
                {
                    ClubId = clubId, Wage = Math.Max(1, WorldBuilder.WageFor(baseOvr, age)), UntilSeason = world.Season + (o.Origin == CareerOrigin.Professional ? 2 : 3),
                    Role = o.Origin == CareerOrigin.Professional ? SquadRole.Starter : o.Origin == CareerOrigin.PromotedYouth ? SquadRole.Rotation : SquadRole.Prospect
                };
                s.InYouthTeam = o.Origin == CareerOrigin.Academy;
            }
            s.AthleteId = athlete.Id;
            s.PreferredPosition = athlete.Position;
            s.Trust = o.Origin == CareerOrigin.Academy ? 35 : o.Origin == CareerOrigin.PromotedYouth ? 45 : 55;
            s.CoachId = world.Club(clubId).CoachId;
            WorldBuilder.UpdateMarketValue(world, athlete);
            NewObjectives(s, rng, true);
            Welcome(s, rng);
            Log.Info("Career", $"Created career {s.Id}: {athlete.DisplayName} ({athlete.Position}, OVR {athlete.Overall}) at {world.Club(clubId).Name}");
            return s;
        }

        private static int PickShirtNumber(WorldState w, string clubId, int wanted, Position pos)
        {
            var used = new HashSet<int>(w.SquadOf(clubId).Select(p => p.ShirtNumber));
            if (wanted > 0 && wanted < 100 && !used.Contains(wanted)) return wanted;
            int[] pref = pos == Position.GK ? new[] { 12, 22, 31 } : pos == Position.ST || pos == Position.CF ? new[] { 9, 19, 29, 39 } : new[] { 17, 18, 20, 21, 27, 28 };
            foreach (var n in pref) if (!used.Contains(n)) return n;
            for (int n = 30; n < 99; n++) if (!used.Contains(n)) return n;
            return 99;
        }

        private static void Welcome(CareerState s, Rng rng)
        {
            var w = s.World;
            var coach = w.Coach(s.CoachId);
            var a = s.Athlete;
            Message(s, coach?.Name ?? "Treinador", "Bem-vindo ao elenco",
                s.Origin == CareerOrigin.Academy
                    ? $"{a.DisplayName}, você começa nas categorias de base. Mostre nos treinos e terá chances no profissional."
                    : $"{a.DisplayName}, conto com você. Trabalhe forte nos treinos e aproveite os minutos que eu der.");
            NewsGenerator.Milestone(s, $"{s.Club.Name} apresenta {a.DisplayName}", rng);
        }

        public static InboxMessage Message(CareerState s, string from, string title, string body, string offerId = null)
        {
            var m = new InboxMessage { Id = s.NewId("m"), Season = s.World.Season, Day = s.Day, From = from, Title = title, Body = body, OfferId = offerId };
            s.Inbox.Insert(0, m);
            if (s.Inbox.Count > 120) s.Inbox.RemoveAt(s.Inbox.Count - 1);
            return m;
        }

        // ================================================================== calendar

        public static IEnumerable<Fixture> AllFixtures(CareerState s) => s.World.Competitions.SelectMany(c => c.Fixtures);

        public static Fixture NextUserFixture(CareerState s) =>
            AllFixtures(s).Where(f => !f.Played && f.Involves(s.ClubId)).OrderBy(f => f.Day).FirstOrDefault();

        public static bool SeasonFinished(CareerState s) =>
            s.World.Competitions.All(c => c.Fixtures.Count == 0 || c.Finished || c.Fixtures.All(f => f.Played) && c.Format == CompetitionFormat.League);

        /// <summary>
        /// Simulates the world day by day until the athlete's next fixture (exclusive) and moves
        /// the calendar to that day. Returns the fixture (or null at season end).
        /// </summary>
        public static Fixture AdvanceToNextMatch(CareerState s)
        {
            var next = NextUserFixture(s);
            int target = next?.Day ?? (AllFixtures(s).Where(f => !f.Played).Select(f => f.Day).DefaultIfEmpty(s.Day).Max() + 1);
            while (s.Day < target)
            {
                SimulateDay(s, s.Day);
                s.Day++;
                DailyTick(s);
                // Knockout draws may have created a user fixture earlier than the planned one.
                var n2 = NextUserFixture(s);
                if (n2 != null && n2 != next && n2.Day < target) { next = n2; target = n2.Day; }
            }
            SimulateDay(s, s.Day, exceptUser: true);
            return NextUserFixture(s);
        }

        /// <summary>Plays all other fixtures scheduled for `day`.</summary>
        public static void SimulateDay(CareerState s, int day, bool exceptUser = false)
        {
            var w = s.World;
            foreach (var comp in w.Competitions)
            {
                var todays = comp.Fixtures.Where(f => !f.Played && f.Day == day && !(exceptUser && f.Involves(s.ClubId))).ToList();
                if (todays.Count == 0) continue;
                bool detailed = comp.Id == w.Club(s.ClubId)?.LeagueId || comp.Format == CompetitionFormat.Knockout;
                foreach (var f in todays)
                {
                    var rng = RngFor(s, f.Id.GetHashCode());
                    if (f.Involves(s.ClubId))
                    {
                        // Missed user fixture (e.g. calendar skipped): simulate including the athlete.
                        var plan = PrepareMatch(s, f);
                        ApplyUserMatch(s, plan, StatSimulator.SimulateDetailed(w, f, rng, plan.Home.StartingXI, plan.Away.StartingXI, plan.OnBench ? s.AthleteId : null, BenchChance(s)));
                        continue;
                    }
                    if (detailed)
                    {
                        var report = StatSimulator.SimulateDetailed(w, f, rng);
                        ApplyReport(s, f, report, rng);
                    }
                    else StatSimulator.SimulateLight(w, f, rng);
                    ResolveKnockoutDraw(s, comp, f, rng);
                }
                if (detailed && comp.Format == CompetitionFormat.League && comp.Id == w.Club(s.ClubId)?.LeagueId)
                    NewsGenerator.RoundUp(s, comp, todays, RngFor(s, 77));
                if (comp.Format == CompetitionFormat.Knockout && WorldBuilder.AdvanceKnockout(comp) && comp.ChampionId != null)
                    OnChampion(s, comp);
            }
            foreach (var comp in w.Competitions.Where(c => c.Format == CompetitionFormat.League && c.ChampionId == null && c.Fixtures.Count > 0 && c.Fixtures.All(f => f.Played)))
            {
                comp.ChampionId = comp.Table()[0].ClubId;
                OnChampion(s, comp);
            }
        }

        private static void ResolveKnockoutDraw(CareerState s, Competition comp, Fixture f, Rng rng)
        {
            if (comp.Format != CompetitionFormat.Knockout || f.HomeGoals != f.AwayGoals || !f.Played) return;
            float sh = s.World.Strength(f.HomeId), sa = s.World.Strength(f.AwayId);
            f.PenaltyWinnerId = rng.Chance(0.5f + (sh - sa) * 0.01f) ? f.HomeId : f.AwayId;
        }

        private static void OnChampion(CareerState s, Competition comp)
        {
            NewsGenerator.Champion(s, comp, RngFor(s, 5));
            if (comp.ChampionId == s.ClubId)
            {
                string title = $"{comp.Name} {s.World.Season}";
                if (!s.Titles.Contains(title)) s.Titles.Add(title);
                s.Trust = MathUtil.Clamp(s.Trust + 5, 0, 100);
            }
        }

        /// <summary>Applies a (simulated or 3D) report to fixture, players and statistics.</summary>
        public static void ApplyReport(CareerState s, Fixture f, MatchReport r, Rng rng)
        {
            var w = s.World;
            f.HomeGoals = r.HomeGoals;
            f.AwayGoals = r.AwayGoals;
            f.Played = true;
            f.SimLevel = r.SimulationLevel;
            f.Goals = r.Goals;
            foreach (var line in r.Players)
            {
                var p = w.Player(line.ProfileId);
                if (p == null) continue;
                var st = w.State(p.Id);
                string clubId = line.Team == 0 ? f.HomeId : f.AwayId;
                var sl = st.Line(w.Season, clubId, f.CompetitionId);
                sl.Apps++;
                if (line.Started) sl.Starts++;
                sl.Minutes += line.Minutes;
                sl.Goals += line.Goals;
                sl.Assists += line.Assists;
                sl.YellowCards += line.YellowCards;
                sl.RedCards += line.RedCards;
                sl.RatingSum += line.Rating;
                if (r.ManOfTheMatchId == p.Id) sl.MotM++;
                int conceded = line.Team == 0 ? r.AwayGoals : r.HomeGoals;
                if (p.Position == Position.GK && conceded == 0 && line.Minutes >= 60) sl.CleanSheets++;
                st.Form = st.Form * 0.7f + line.Rating * 0.3f;
                // Discipline.
                if (line.RedCards > 0) st.SuspensionMatches = Math.Max(st.SuspensionMatches, rng.Range(1, 3));
                else if (line.YellowCards > 0 && ++st.YellowAccumulation >= 3) { st.SuspensionMatches = 1; st.YellowAccumulation = 0; }
                if (line.Injured && st.InjuryDays <= 0)
                {
                    string[] names = { "lesão muscular na coxa", "entorse no tornozelo", "contusão no joelho", "lesão na panturrilha" };
                    int days = rng.Range(5, 40);
                    st.InjuryDays = days;
                    st.InjuryName = names[rng.Range(0, names.Length)];
                    if (p.Id == s.AthleteId || p.ClubId == s.ClubId) NewsGenerator.Injury(s, p.Id, st.InjuryName, days, rng);
                }
            }
            // Players who did not play serve suspensions.
            foreach (var clubId in new[] { f.HomeId, f.AwayId })
                foreach (var p in w.SquadOf(clubId))
                {
                    var st = w.State(p.Id);
                    if (st.SuspensionMatches > 0 && r.Line(p.Id) == null) st.SuspensionMatches--;
                }
        }

        private static void DailyTick(CareerState s)
        {
            var w = s.World;
            foreach (var st in w.PlayerStates.Values)
                if (st.InjuryDays > 0) st.InjuryDays--;
            foreach (var o in s.Offers.Where(o => o.Status == OfferStatus.Pending && o.ExpiresDay < s.Day))
            {
                o.Status = OfferStatus.Expired;
            }
            if (s.Day % 7 == 0) WeeklyTick(s);
            if (s.Day == 1 || s.Day == TransferWindow2Start) TransferWindowOpens(s);
            if (s.Day > 0 && s.Day % 28 == 0) CoachReview(s);
            if (InternationalWindows.Contains(s.Day)) NationalTeamWindow(s);
        }

        // ================================================================== selection & user match

        /// <summary>Builds both team sheets and decides whether the athlete starts.</summary>
        public static MatchPlan PrepareMatch(CareerState s, Fixture f)
        {
            var w = s.World;
            var rng = RngFor(s, 911);
            var plan = new MatchPlan { Fixture = f, Competition = w.Competition(f.CompetitionId), UserTeam = f.HomeId == s.ClubId ? 0 : 1 };
            var athleteState = s.AthleteState;
            bool available = w.Available(s.AthleteId) && !s.Retired;
            string force = null, bench = null;

            if (!available)
            {
                plan.Unavailable = true;
                plan.Reason = athleteState.InjuryDays > 0 ? $"Lesionado ({athleteState.InjuryName}, {athleteState.InjuryDays} dias)" : "Suspenso";
            }
            else if (s.InYouthTeam)
            {
                plan.Unavailable = true;
                plan.Reason = "Você ainda está nas categorias de base";
            }
            else
            {
                plan.Starter = CoachPicksAthlete(s, rng, out plan.Reason);
                if (plan.Starter) force = s.AthleteId; else bench = s.AthleteId;
                plan.OnBench = !plan.Starter;
            }

            TeamSheet Sheet(string clubId, bool home)
            {
                var c = w.Club(clubId);
                var squad = w.SquadOf(clubId).Where(p => w.Available(p.Id) && !(p.Id == s.AthleteId && (plan.Unavailable))).ToList();
                if (squad.Count < 11) squad = w.SquadOf(clubId).ToList();
                var kit = home ? c.HomeKit : (DemoClubs.Clash(c.HomeKit.Shirt, w.Club(home ? f.AwayId : f.HomeId).HomeKit.Shirt) ? c.AwayKit : c.HomeKit);
                var sheet = TeamSheetBuilder.Build(c.Id, c.Name, c.ShortName, squad, c.Formation, kit, c.Tactics, null,
                    clubId == s.ClubId ? force : null, clubId == s.ClubId ? bench : null);
                sheet.AiControlledCoach = true;
                return sheet;
            }
            plan.Home = Sheet(f.HomeId, true);
            plan.Away = Sheet(f.AwayId, false);
            return plan;
        }

        /// <summary>The coach compares the athlete with the incumbent for his best slot.</summary>
        public static bool CoachPicksAthlete(CareerState s, Rng rng, out string reason)
        {
            var w = s.World;
            var club = s.Club;
            var me = s.Athlete;
            var squad = w.SquadOf(club.Id).Where(p => w.Available(p.Id)).ToList();
            var baseline = TeamSheetBuilder.Build(club.Id, club.Name, club.ShortName, squad.Where(p => p.Id != me.Id).ToList(), club.Formation, club.HomeKit);
            var formation = Formation.Get(club.Formation);
            int bestSlot = 0; float bestFit = float.MinValue;
            for (int i = 0; i < formation.Slots.Length; i++)
            {
                float fit = TeamSheetBuilder.Fit(me, formation.Slots[i].Position) + (formation.Slots[i].Position == s.PreferredPosition ? 2f : 0f);
                if (fit > bestFit) { bestFit = fit; bestSlot = i; }
            }
            var incumbent = baseline.StartingXI[bestSlot];
            var coach = w.Coach(club.CoachId);
            float youth = me.AgeOn(s.Date) <= 21 ? (coach?.YouthTrust ?? 0.5f) * 4f - 2f : 0f;
            float myScore = bestFit + (s.Trust - 50f) / 7f + (s.AthleteState.Form - 6.5f) * 2.5f + youth + rng.Gaussian() * 1.2f;
            float incScore = TeamSheetBuilder.Fit(incumbent, formation.Slots[bestSlot].Position) + (w.State(incumbent.Id).Form - 6.5f) * 2.5f + rng.Gaussian() * 1.2f;
            if (s.StartRequestPending) { myScore += 1.5f; s.StartRequestPending = false; }
            bool start = myScore >= incScore;
            reason = start ? $"Titular na posição {formation.Slots[bestSlot].Position}" : $"No banco: {incumbent.ShortName} começa como {formation.Slots[bestSlot].Position}";
            return start;
        }

        /// <summary>Chance the coach brings the benched athlete on (for simulated matches).</summary>
        public static float BenchChance(CareerState s) => MathUtil.Clamp(0.25f + (s.Trust - 40f) / 100f, 0.15f, 0.8f);

        /// <summary>Applies a user-club match (3D or simulated): stats, XP, trust, objectives, news.</summary>
        public static void ApplyUserMatch(CareerState s, MatchPlan plan, MatchReport report)
        {
            var w = s.World;
            var f = plan.Fixture;
            var rng = RngFor(s, 4242);
            ApplyReport(s, f, report, rng);
            ResolveKnockoutDraw(s, plan.Competition, f, rng);
            var line = report.Line(s.AthleteId);
            bool home = f.HomeId == s.ClubId;
            int gf = home ? f.HomeGoals : f.AwayGoals, ga = home ? f.AwayGoals : f.HomeGoals;

            var log = new AthleteMatchLog
            {
                Season = w.Season, Day = s.Day, CompetitionId = f.CompetitionId, OpponentId = home ? f.AwayId : f.HomeId, Home = home,
                GoalsFor = gf, GoalsAgainst = ga, Minutes = line?.Minutes ?? 0, Goals = line?.Goals ?? 0, Assists = line?.Assists ?? 0,
                Rating = line?.Rating ?? 0, Started = line?.Started ?? false, Level = report.SimulationLevel
            };
            s.MatchLog.Insert(0, log);
            if (s.MatchLog.Count > 400) s.MatchLog.RemoveAt(s.MatchLog.Count - 1);

            if (line != null && line.Minutes > 0)
            {
                int xp = (int)(line.Minutes / 90f * (40 + (line.Rating - 6f) * 35)) + line.Goals * 15 + line.Assists * 10 + (report.ManOfTheMatchId == s.AthleteId ? 20 : 0);
                GainXp(s, Math.Max(5, xp));
                float expected = line.Started ? 6.6f : 6.3f;
                s.Trust = MathUtil.Clamp(s.Trust + (line.Rating - expected) * 4f + line.Goals * 1.2f + line.Assists * 0.6f, 0, 100);
                var st = s.AthleteState;
                st.Morale = MathUtil.Clamp01(st.Morale + (line.Rating - 6.5f) * 0.05f + (gf > ga ? 0.03f : gf < ga ? -0.03f : 0f));
                Milestones(s, rng);
            }
            else if (plan.OnBench)
            {
                s.AthleteState.Morale = MathUtil.Clamp01(s.AthleteState.Morale - 0.03f);
                int streak = s.MatchLog.TakeWhile(m => !m.Started).Count();
                if (streak == 3) NewsGenerator.LostPlace(s, streak, rng);
            }
            UpdateObjectives(s);
            NewsGenerator.AfterUserMatch(s, f, report, rng);
            CoachFeedback(s, line, gf, ga, rng);
            if (plan.Competition.Format == CompetitionFormat.Knockout && WorldBuilder.AdvanceKnockout(plan.Competition) && plan.Competition.ChampionId != null)
                OnChampion(s, plan.Competition);
            foreach (var p in w.SquadOf(s.ClubId)) WorldBuilder.UpdateMarketValue(w, p);
        }

        /// <summary>Simulates the athlete's match instead of playing it in 3D.</summary>
        public static MatchReport SimulateUserMatch(CareerState s, MatchPlan plan)
        {
            var rng = RngFor(s, 31337);
            var report = StatSimulator.SimulateDetailed(s.World, plan.Fixture, rng, plan.Home.StartingXI, plan.Away.StartingXI, plan.OnBench ? s.AthleteId : null, BenchChance(s));
            ApplyUserMatch(s, plan, report);
            return report;
        }

        private static void CoachFeedback(CareerState s, PlayerMatchLine line, int gf, int ga, Rng rng)
        {
            var coach = s.World.Coach(s.Club.CoachId);
            string from = coach?.Name ?? "Treinador";
            if (line == null || line.Minutes == 0)
            {
                if (rng.Chance(0.35f)) Message(s, from, "Paciência", "Continue treinando forte. Sua chance vai chegar se mantiver o nível nos treinos.");
                return;
            }
            string text = line.Rating >= 7.5f ? $"Grande jogo (nota {line.Rating:0.0}). É exatamente isso que espero de você."
                : line.Rating >= 6.6f ? $"Boa partida (nota {line.Rating:0.0}). Mantenha a consistência."
                : line.Rating >= 6.0f ? $"Jogo regular (nota {line.Rating:0.0}). Precisa participar mais e errar menos."
                : $"Atuação abaixo (nota {line.Rating:0.0}). Precisamos conversar sobre seu posicionamento.";
            Message(s, from, "Avaliação pós-jogo", text);
        }

        private static void Milestones(CareerState s, Rng rng)
        {
            var all = s.AthleteState.Stats;
            int goals = all.Sum(l => l.Goals), apps = all.Sum(l => l.Apps);
            int lastGoals = goals - (s.MatchLog.Count > 0 ? s.MatchLog[0].Goals : 0);
            foreach (int m in new[] { 1, 10, 25, 50, 100, 200 })
                if (lastGoals < m && goals >= m) NewsGenerator.Milestone(s, m == 1 ? $"Primeiro gol como profissional de {s.Athlete.DisplayName}!" : $"{s.Athlete.DisplayName} chega a {m} gols na carreira", rng);
            if (apps == 1) NewsGenerator.Milestone(s, $"Estreia de {s.Athlete.DisplayName} pelo {s.Club.Name}", rng);
            if (apps == 50 || apps == 100 || apps == 250) NewsGenerator.Milestone(s, $"{s.Athlete.DisplayName} completa {apps} jogos na carreira", rng);
        }
    }
}
