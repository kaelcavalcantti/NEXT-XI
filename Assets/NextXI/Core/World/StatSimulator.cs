using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core.Data;
using NextXI.Core.Match;

namespace NextXI.Core.World
{
    /// <summary>
    /// Statistical match simulation for fixtures that are not played in 3D.
    /// Detailed: line-ups, Poisson goals from attack/defence strength and home advantage, scorers
    /// and assisters weighted by attributes and position, ratings, cards and injuries.
    /// Light: only the score (distant competitions). Both keep tables and statistics coherent.
    /// </summary>
    public static class StatSimulator
    {
        public const float HomeAdvantage = 0.18f;

        public static MatchReport SimulateDetailed(WorldState w, Fixture f, Rng rng, IList<PlayerProfile> homeXI = null, IList<PlayerProfile> awayXI = null,
                                                   string forcedBenchPlayerId = null, float forcedBenchMinutesChance = 0.45f)
        {
            var home = homeXI ?? PickXI(w, f.HomeId);
            var away = awayXI ?? PickXI(w, f.AwayId);
            float attH = Attack(home), defH = Defence(home), attA = Attack(away), defA = Defence(away);
            float lh = ExpectedGoals(attH, defA, true);
            float la = ExpectedGoals(attA, defH, false);
            int gh = rng.Poisson(lh), ga = rng.Poisson(la);

            var report = new MatchReport { HomeClubId = f.HomeId, AwayClubId = f.AwayId, HomeGoals = gh, AwayGoals = ga, SimulationLevel = "detailed" };
            var lines = new Dictionary<string, PlayerMatchLine>();
            void AddLines(IList<PlayerProfile> xi, int team)
            {
                foreach (var p in xi)
                    lines[p.Id] = new PlayerMatchLine { ProfileId = p.Id, Team = team, Started = true, Minutes = 90, Rating = 6.3f + rng.Gaussian() * 0.45f + (p.Overall - 70) * 0.015f };
            }
            AddLines(home, 0);
            AddLines(away, 1);

            // Substitutions: ~3 per team around 60-80'.
            Substitutions(w, f.HomeId, home, 0, lines, rng, forcedBenchPlayerId, forcedBenchMinutesChance);
            Substitutions(w, f.AwayId, away, 1, lines, rng, forcedBenchPlayerId, forcedBenchMinutesChance);

            var minutes = new List<int>();
            for (int i = 0; i < gh + ga; i++) minutes.Add(rng.Range(1, 95));
            minutes.Sort();
            int hLeft = gh, aLeft = ga;
            foreach (int min in minutes)
            {
                bool homeGoal = hLeft > 0 && (aLeft == 0 || rng.Chance(hLeft / (float)(hLeft + aLeft)));
                if (homeGoal) hLeft--; else aLeft--;
                int team = homeGoal ? 0 : 1;
                var pool = lines.Values.Where(l => l.Team == team && OnPitchAt(l, min)).ToList();
                var scorer = WeightedPick(w, pool, rng, ScorerWeight);
                PlayerMatchLine assist = null;
                if (rng.Chance(0.72f)) assist = WeightedPick(w, pool.Where(l => l != scorer).ToList(), rng, AssistWeight);
                bool own = rng.Chance(0.03f);
                if (own)
                {
                    var opp = lines.Values.Where(l => l.Team != team && OnPitchAt(l, min)).ToList();
                    scorer = opp.Count > 0 ? opp[rng.Range(0, opp.Count)] : scorer;
                    assist = null;
                }
                else if (scorer != null) { scorer.Goals++; scorer.Shots++; scorer.ShotsOnTarget++; scorer.Rating += 0.9f; }
                if (assist != null) { assist.Assists++; assist.KeyPasses++; assist.Rating += 0.55f; }
                report.Goals.Add(new GoalRecord { Minute = Math.Min(min, 90), Team = team, ScorerId = scorer?.ProfileId, AssistId = assist?.ProfileId, OwnGoal = own });
            }

            // Shots/saves/cards/defensive contributions for realism of season stats.
            foreach (var l in lines.Values)
            {
                var p = w.Player(l.ProfileId);
                if (p == null) continue;
                var g = PositionInfo.Group(p.Position);
                float k = l.Minutes / 90f;
                l.Shots += rng.Poisson((g == PositionGroup.Forward ? 2.2f : g == PositionGroup.Midfielder ? 1.0f : 0.3f) * k);
                l.ShotsOnTarget = Math.Min(l.Shots, l.ShotsOnTarget + rng.Poisson(l.Shots * 0.35f));
                l.Passes = rng.Poisson((g == PositionGroup.Midfielder ? 48f : g == PositionGroup.Defender ? 42f : g == PositionGroup.Goalkeeper ? 25f : 22f) * k);
                l.PassesCompleted = (int)(l.Passes * MathUtil.Clamp(0.62f + p.Attributes.ShortPassing / 400f + rng.Gaussian() * 0.04f, 0.5f, 0.95f));
                l.Tackles = rng.Poisson((g == PositionGroup.Defender ? 2.5f : g == PositionGroup.Midfielder ? 2f : 0.6f) * k);
                l.TacklesWon = Math.Min(l.Tackles, rng.Poisson(l.Tackles * 0.6f));
                l.Interceptions = rng.Poisson((g == PositionGroup.Defender ? 1.6f : 0.7f) * k);
                if (rng.Chance(0.11f * k)) { l.YellowCards = 1; l.Rating -= 0.25f; }
                if (rng.Chance(0.006f * k)) { l.RedCards = 1; l.Rating -= 1.2f; }
                if (g == PositionGroup.Goalkeeper)
                {
                    int conceded = l.Team == 0 ? ga : gh;
                    l.Saves = rng.Poisson(2.6f);
                    l.Rating += l.Saves * 0.15f - conceded * 0.3f + (conceded == 0 ? 0.5f : 0f);
                }
                else if (g == PositionGroup.Defender) l.Rating -= (l.Team == 0 ? ga : gh) * 0.12f;
                int diff = l.Team == 0 ? gh - ga : ga - gh;
                l.Rating += MathUtil.Clamp(diff, -3, 3) * 0.12f;
                l.Rating = (float)Math.Round(MathUtil.Clamp(l.Rating, 3.5f, 10f), 1);
                if (rng.Chance(0.004f * k)) l.Injured = true;
            }
            report.Players = lines.Values.ToList();
            report.HomeStats = new TeamMatchStats { Goals = gh, Shots = lines.Values.Where(l => l.Team == 0).Sum(l => l.Shots), ShotsOnTarget = lines.Values.Where(l => l.Team == 0).Sum(l => l.ShotsOnTarget) };
            report.AwayStats = new TeamMatchStats { Goals = ga, Shots = lines.Values.Where(l => l.Team == 1).Sum(l => l.Shots), ShotsOnTarget = lines.Values.Where(l => l.Team == 1).Sum(l => l.ShotsOnTarget) };
            report.ManOfTheMatchId = lines.Values.OrderByDescending(l => l.Rating).First().ProfileId;
            return report;
        }

        /// <summary>Light simulation: only goals.</summary>
        public static void SimulateLight(WorldState w, Fixture f, Rng rng)
        {
            float sh = w.Strength(f.HomeId), sa = w.Strength(f.AwayId);
            f.HomeGoals = rng.Poisson(ExpectedGoals(sh, sa, true));
            f.AwayGoals = rng.Poisson(ExpectedGoals(sa, sh, false));
            f.Played = true;
            f.SimLevel = "light";
        }

        public static float ExpectedGoals(float attack, float defence, bool home)
        {
            float d = (attack - defence) / 9f + (home ? HomeAdvantage : -HomeAdvantage * 0.4f);
            return MathUtil.Clamp(1.32f * MathF.Exp(d), 0.15f, 5f);
        }

        public static float Attack(IList<PlayerProfile> xi)
        {
            float s = 0, n = 0;
            foreach (var p in xi)
            {
                var g = PositionInfo.Group(p.Position);
                float w = g == PositionGroup.Forward ? 1.6f : g == PositionGroup.Midfielder ? 1.1f : g == PositionGroup.Defender ? 0.4f : 0f;
                s += p.Overall * w; n += w;
            }
            return n > 0 ? s / n : 50;
        }

        public static float Defence(IList<PlayerProfile> xi)
        {
            float s = 0, n = 0;
            foreach (var p in xi)
            {
                var g = PositionInfo.Group(p.Position);
                float w = g == PositionGroup.Defender ? 1.5f : g == PositionGroup.Goalkeeper ? 1.4f : g == PositionGroup.Midfielder ? 0.9f : 0.3f;
                s += p.Overall * w; n += w;
            }
            return n > 0 ? s / n : 50;
        }

        public static List<PlayerProfile> PickXI(WorldState w, string clubId, string forceStarter = null, string excludeId = null)
        {
            var c = w.Club(clubId);
            var squad = w.SquadOf(clubId).Where(p => w.Available(p.Id) && p.Id != excludeId).ToList();
            if (squad.Count < 11) squad = w.SquadOf(clubId).Where(p => p.Id != excludeId).ToList();
            var sheet = TeamSheetBuilder.Build(c.Id, c.Name, c.ShortName, squad, c.Formation, c.HomeKit, c.Tactics, null, forceStarter);
            return sheet.StartingXI;
        }

        private static void Substitutions(WorldState w, string clubId, IList<PlayerProfile> xi, int team, Dictionary<string, PlayerMatchLine> lines, Rng rng, string forcedBench, float forcedChance)
        {
            var bench = w.SquadOf(clubId).Where(p => w.Available(p.Id) && !xi.Contains(p) && p.Position != Position.GK).OrderByDescending(p => p.Overall).Take(7).ToList();
            int subs = rng.Range(2, 5);
            var outfield = xi.Where(p => p.Position != Position.GK).ToList();
            var benchOrder = bench.ToList();
            var forced = benchOrder.FirstOrDefault(p => p.Id == forcedBench);
            if (forced != null) { benchOrder.Remove(forced); if (rng.Chance(forcedChance)) benchOrder.Insert(0, forced); }
            for (int i = 0; i < subs && i < benchOrder.Count && outfield.Count > 0; i++)
            {
                var on = benchOrder[i];
                var group = PositionInfo.Group(on.Position);
                var off = outfield.FirstOrDefault(p => PositionInfo.Group(p.Position) == group) ?? outfield[0];
                outfield.Remove(off);
                int minute = rng.Range(55, 86);
                if (lines.TryGetValue(off.Id, out var lo)) lo.Minutes = minute;
                lines[on.Id] = new PlayerMatchLine { ProfileId = on.Id, Team = team, Started = false, Minutes = 90 - minute, Rating = 6.3f + rng.Gaussian() * 0.35f };
            }
        }

        private static bool OnPitchAt(PlayerMatchLine l, int minute) => l.Started ? minute <= l.Minutes : minute >= 90 - l.Minutes;

        private static float ScorerWeight(PlayerProfile p)
        {
            var a = p.Attributes;
            switch (PositionInfo.Group(p.Position))
            {
                case PositionGroup.Forward: return 6f + (a.Finishing + a.Positioning - 120) * 0.12f;
                case PositionGroup.Midfielder: return p.Position == Position.CAM ? 3f : p.Position == Position.CDM ? 0.8f : 1.8f;
                case PositionGroup.Defender: return 0.5f + a.Heading / 200f;
                default: return 0.01f;
            }
        }

        private static float AssistWeight(PlayerProfile p)
        {
            var a = p.Attributes;
            switch (PositionInfo.Group(p.Position))
            {
                case PositionGroup.Forward: return 2.5f + a.Vision / 50f;
                case PositionGroup.Midfielder: return 3f + (a.Vision + a.ShortPassing - 120) * 0.05f;
                case PositionGroup.Defender: return p.Position == Position.CB ? 0.5f : 1.4f + a.Crossing / 80f;
                default: return 0.05f;
            }
        }

        private static PlayerMatchLine WeightedPick(WorldState w, List<PlayerMatchLine> pool, Rng rng, Func<PlayerProfile, float> weight)
        {
            if (pool.Count == 0) return null;
            float total = 0;
            var ws = new float[pool.Count];
            for (int i = 0; i < pool.Count; i++)
            {
                var p = w.Player(pool[i].ProfileId);
                ws[i] = p == null ? 0.1f : MathF.Max(0.01f, weight(p)) * MathF.Max(0.2f, pool[i].Minutes / 90f);
                total += ws[i];
            }
            float r = rng.Next() * total;
            for (int i = 0; i < pool.Count; i++) { r -= ws[i]; if (r <= 0) return pool[i]; }
            return pool[pool.Count - 1];
        }
    }
}
