using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NextXI.Core;
using NextXI.Core.Data;
using NextXI.Core.Match;
using NextXI.Core.World;

namespace NextXI.Tests
{
    public class WorldTests
    {
        [Test]
        public void DoubleRoundRobinIsComplete()
        {
            var clubs = Enumerable.Range(0, 10).Select(i => "c" + i).ToList();
            var fx = Scheduler.DoubleRoundRobin("L", clubs, 7, 7, new Rng(5));
            Assert.AreEqual(90, fx.Count);
            foreach (var a in clubs)
                foreach (var b in clubs.Where(b => b != a))
                    Assert.AreEqual(1, fx.Count(f => f.HomeId == a && f.AwayId == b), $"{a} vs {b} at home must happen once");
            foreach (var round in fx.GroupBy(f => f.Round))
            {
                var teams = round.SelectMany(f => new[] { f.HomeId, f.AwayId }).ToList();
                Assert.AreEqual(teams.Count, teams.Distinct().Count(), "a club plays twice in one round");
                Assert.AreEqual(1, round.Select(f => f.Day).Distinct().Count());
            }
        }

        [Test]
        public void OddNumberOfClubsGetsByes()
        {
            var clubs = Enumerable.Range(0, 7).Select(i => "c" + i).ToList();
            var fx = Scheduler.DoubleRoundRobin("L", clubs, 0, 7, new Rng(1));
            Assert.AreEqual(42, fx.Count);
        }

        [Test]
        public void TableComputesPointsAndOrdering()
        {
            var c = new Competition { Id = "L", ClubIds = new List<string> { "a", "b", "c" } };
            c.Fixtures.Add(new Fixture { HomeId = "a", AwayId = "b", Played = true, HomeGoals = 2, AwayGoals = 0 });
            c.Fixtures.Add(new Fixture { HomeId = "b", AwayId = "c", Played = true, HomeGoals = 1, AwayGoals = 1 });
            c.Fixtures.Add(new Fixture { HomeId = "c", AwayId = "a", Played = true, HomeGoals = 3, AwayGoals = 1 });
            c.Fixtures.Add(new Fixture { HomeId = "a", AwayId = "c", Played = false });
            var t = c.Table();
            Assert.AreEqual("c", t[0].ClubId); // 4 pts, GD +2
            Assert.AreEqual(4, t[0].Points);
            Assert.AreEqual("a", t[1].ClubId); // 3 pts
            Assert.AreEqual(1, t[2].Points);
            Assert.AreEqual(2, t[0].Played);
        }

        [Test]
        public void DemoWorldIsConsistent()
        {
            var w = WorldBuilder.CreateDemoWorld(2026, 1);
            Assert.GreaterOrEqual(w.Clubs.Count, 18);
            foreach (var c in w.Clubs.Values)
            {
                Assert.GreaterOrEqual(c.Squad.Count, 18, c.Name);
                Assert.IsNotNull(w.Coach(c.CoachId));
                Assert.IsTrue(c.IsFictional);
                var xi = StatSimulator.PickXI(w, c.Id);
                Assert.AreEqual(11, xi.Count);
                Assert.AreEqual(Position.GK, xi[0].Position);
                foreach (var id in c.Squad) Assert.AreEqual(c.Id, w.Player(id).ClubId);
            }
            Assert.IsTrue(w.Players.Values.All(p => p.IsFictional));
            Assert.IsTrue(w.DataSources.Any(d => d.Contains("fictional")));
            Assert.Greater(w.Competition(WorldBuilder.BrazilLeague).Fixtures.Count, 0);
        }

        [Test]
        public void SearchFindsPlayersByFilters()
        {
            var w = WorldBuilder.CreateDemoWorld(2026, 2);
            var club = w.Clubs.Values.First();
            var anyPlayer = w.Player(club.Squad[0]);
            Assert.IsTrue(w.Search(text: anyPlayer.LastName).Any(p => p.Id == anyPlayer.Id));
            Assert.IsTrue(w.Search(clubId: club.Id).All(p => p.ClubId == club.Id));
            Assert.IsTrue(w.Search(pos: Position.GK).All(p => p.CanPlay(Position.GK)));
            Assert.IsTrue(w.Search(competitionId: WorldBuilder.BrazilLeague).All(p => w.Competition(WorldBuilder.BrazilLeague).ClubIds.Contains(p.ClubId)));
        }

        [Test]
        public void KnockoutProducesAChampion()
        {
            var w = WorldBuilder.CreateDemoWorld(2026, 3);
            var cup = w.Competition(WorldBuilder.NationalCup);
            var rng = new Rng(9);
            int guard = 0;
            while (cup.ChampionId == null && guard++ < 10)
            {
                foreach (var f in cup.Fixtures.Where(f => !f.Played))
                {
                    StatSimulator.SimulateLight(w, f, rng);
                    if (f.HomeGoals == f.AwayGoals) f.PenaltyWinnerId = f.HomeId;
                }
                WorldBuilder.AdvanceKnockout(cup);
            }
            Assert.IsNotNull(cup.ChampionId);
            Assert.AreEqual(4 + 2 + 1, cup.Fixtures.Count);
        }

        [Test]
        public void DetailedSimulationIsCoherent()
        {
            var w = WorldBuilder.CreateDemoWorld(2026, 4);
            var comp = w.Competition(WorldBuilder.BrazilLeague);
            var rng = new Rng(4);
            int goals = 0, n = 0, homeWins = 0, awayWins = 0;
            foreach (var f in comp.Fixtures.Concat(comp.Fixtures).Concat(comp.Fixtures))
            {
                var r = StatSimulator.SimulateDetailed(w, f, rng);
                Assert.AreEqual(r.HomeGoals + r.AwayGoals, r.Goals.Count);
                int scorersHome = r.Goals.Count(g => g.Team == 0);
                Assert.AreEqual(r.HomeGoals, scorersHome);
                Assert.AreEqual(r.Goals.Count(g => !g.OwnGoal), r.Players.Sum(p => p.Goals));
                Assert.IsTrue(r.Players.Count(p => p.Team == 0 && p.Started) == 11);
                Assert.IsTrue(r.Players.All(p => p.Rating >= 3.5f && p.Rating <= 10f));
                goals += r.HomeGoals + r.AwayGoals; n++;
                if (r.HomeGoals > r.AwayGoals) homeWins++; else if (r.AwayGoals > r.HomeGoals) awayWins++;
            }
            Assert.That(goals / (float)n, Is.InRange(1.8f, 3.8f));
            Assert.Greater(homeWins, awayWins, "home advantage");
        }

        [Test]
        public void ImportAddsClubsPlayersAndLeagues()
        {
            var w = WorldBuilder.CreateDemoWorld(2026, 5);
            var pack = new WorldPack
            {
                PackId = "test-pack", Source = "Unit test", License = "test", UpdatedAt = "2026-10-01", Fictional = true,
                Clubs = { new ClubRecord { Id = "imp-1", Name = "Imported FC", ShortName = "IMP" } },
                Players = { new PlayerProfile { Id = "imp-p1", FirstName = "Ana", LastName = "Teste", ClubId = "imp-1", Position = Position.ST } },
                Leagues = { new LeagueDef { Id = "imp-league", Name = "Imported League", ClubIds = { "imp-1" } } }
            };
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(pack, NextXI.Core.Persistence.SaveSystem.JsonSettings);
            var errors = WorldBuilder.Import(w, WorldBuilder.ParsePack(json));
            Assert.IsEmpty(errors);
            Assert.IsNotNull(w.Club("imp-1"));
            Assert.Contains("imp-p1", w.Club("imp-1").Squad);
            Assert.IsNotNull(w.Competition("imp-league"));
            Assert.IsTrue(w.DataSources.Any(d => d.Contains("test-pack")));
        }

        [Test]
        public void RealDataPackWithoutProvenanceIsRejected()
        {
            var w = WorldBuilder.CreateDemoWorld(2026, 6);
            var errors = WorldBuilder.Import(w, new WorldPack { PackId = "real", Fictional = false, Clubs = { new ClubRecord { Id = "x", Name = "X" } } });
            Assert.IsNotEmpty(errors);
            Assert.IsNull(w.Club("x"));
        }

        [Test]
        public void ImportReportsBrokenReferences()
        {
            var w = WorldBuilder.CreateDemoWorld(2026, 7);
            var errors = WorldBuilder.Import(w, new WorldPack { PackId = "p", Source = "s", Players = { new PlayerProfile { Id = "orphan", ClubId = "nope" } } });
            Assert.IsTrue(errors.Any(e => e.Contains("unknown club")));
        }
    }
}
