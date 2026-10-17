using System.Linq;
using NUnit.Framework;
using NextXI.Core;
using NextXI.Core.Career;
using NextXI.Core.Data;
using NextXI.Core.Match;
using NextXI.Core.Persistence;
using NextXI.Core.World;

namespace NextXI.Tests
{
    public class CareerTests
    {
        private static CareerState NewCareer(CareerOrigin origin = CareerOrigin.Professional, Position pos = Position.ST, ulong seed = 42) =>
            CareerManager.Create(new CreationOptions { FirstName = "Teste", LastName = "Silva", Position = pos, ArchetypeId = pos == Position.GK ? "agile-keeper" : "poacher", Origin = origin, Seed = seed });

        [Test]
        public void CreatingAnAthleteRegistersHimAtTheClub()
        {
            var s = NewCareer();
            Assert.IsNotNull(s.Athlete);
            Assert.Contains(s.AthleteId, s.Club.Squad);
            Assert.AreEqual(s.ClubId, s.Athlete.ClubId);
            Assert.That(s.Athlete.Overall, Is.InRange(64, 74));
            Assert.Greater(s.AthleteState.Contract.UntilSeason, 2026);
            Assert.IsNotEmpty(s.Objectives);
            Assert.IsNotEmpty(s.News);
            Assert.IsNotEmpty(s.Inbox);
            Assert.AreNotEqual(0, s.Athlete.ShirtNumber);
            Assert.AreEqual(s.Club.Squad.Count, s.Club.Squad.Distinct().Count());
        }

        [Test]
        public void ArchetypeShapesStartingAttributes()
        {
            var speed = CareerManager.Create(new CreationOptions { Position = Position.RW, ArchetypeId = "speed-winger", Origin = CareerOrigin.Professional, Seed = 7 });
            var target = CareerManager.Create(new CreationOptions { Position = Position.ST, ArchetypeId = "target-forward", Origin = CareerOrigin.Professional, Seed = 7 });
            Assert.Greater(speed.Athlete.Attributes.Pace, target.Athlete.Attributes.Pace);
            Assert.Greater(target.Athlete.Attributes.Heading, speed.Athlete.Attributes.Heading);
        }

        [Test]
        public void AdvanceAndPrepareMatch()
        {
            var s = NewCareer();
            var f = CareerManager.AdvanceToNextMatch(s);
            Assert.IsNotNull(f);
            Assert.AreEqual(f.Day, s.Day);
            Assert.IsTrue(f.Involves(s.ClubId));
            var plan = CareerManager.PrepareMatch(s, f);
            Assert.AreEqual(11, plan.Home.StartingXI.Count);
            Assert.AreEqual(11, plan.Away.StartingXI.Count);
            var mine = plan.UserTeam == 0 ? plan.Home : plan.Away;
            if (plan.Starter) Assert.IsTrue(mine.StartingXI.Any(p => p.Id == s.AthleteId));
            else Assert.IsTrue(mine.Bench.Any(p => p.Id == s.AthleteId) || plan.Unavailable);
            // Other fixtures before this day were simulated.
            Assert.IsTrue(CareerManager.AllFixtures(s).Where(x => x.Day < s.Day).All(x => x.Played));
        }

        [Test]
        public void SimulatedUserMatchUpdatesEverything()
        {
            var s = NewCareer();
            var f = CareerManager.AdvanceToNextMatch(s);
            var plan = CareerManager.PrepareMatch(s, f);
            int news = s.News.Count;
            CareerManager.SimulateUserMatch(s, plan);
            Assert.IsTrue(f.Played);
            Assert.AreEqual(1, s.MatchLog.Count);
            Assert.Greater(s.News.Count, news);
            var table = s.World.Competition(f.CompetitionId);
            if (table.Format == CompetitionFormat.League)
                Assert.AreEqual(1, table.Table().First(r => r.ClubId == s.ClubId).Played);
        }

        [Test]
        public void ThreeDimensionalMatchReportFeedsTheCareer()
        {
            var s = NewCareer();
            var f = CareerManager.AdvanceToNextMatch(s);
            var plan = CareerManager.PrepareMatch(s, f);
            var settings = new MatchSettings { RealMinutesPerHalf = 3, Seed = 5, ControlMode = ControlMode.PlayerLock };
            var engine = new MatchEngine(plan.Home, plan.Away, settings);
            engine.SetUserTeam(plan.UserTeam, s.AthleteId);
            engine.UserAutopilot = true; // headless stand-in for the user's play
            engine.RunToEnd();
            var report = MatchReport.FromEngine(engine);
            Assert.AreEqual(engine.Score(0), report.HomeGoals);
            CareerManager.ApplyUserMatch(s, plan, report);
            Assert.IsTrue(f.Played);
            Assert.AreEqual(report.HomeGoals, f.HomeGoals);
            if (plan.Starter)
            {
                Assert.Greater(s.MatchLog[0].Minutes, 0);
                Assert.Greater(s.AthleteState.Stats.Sum(l => l.Apps), 0);
            }
        }

        [Test]
        public void FullSeasonAndRollover()
        {
            var s = NewCareer(seed: 99);
            int guard = 0;
            while (guard++ < 80)
            {
                var f = CareerManager.AdvanceToNextMatch(s);
                if (f == null) break;
                CareerManager.SimulateUserMatch(s, CareerManager.PrepareMatch(s, f));
            }
            CareerManager.AdvanceToNextMatch(s); // play out remaining fixtures
            foreach (var c in s.World.Competitions) Assert.IsTrue(c.Fixtures.All(x => x.Played), c.Name + " has unplayed fixtures");
            Assert.IsNotNull(s.World.Competition(WorldBuilder.BrazilLeague).ChampionId);
            Assert.IsNotNull(s.World.Competition(WorldBuilder.NationalCup).ChampionId);
            int apps = s.AthleteState.Stats.Where(l => l.Season == 2026).Sum(l => l.Apps);
            Assert.Greater(apps, 0, "athlete never played");
            CareerManager.EndSeason(s);
            Assert.AreEqual(2027, s.World.Season);
            Assert.AreEqual(0, s.Day);
            Assert.IsTrue(s.World.Competition(WorldBuilder.BrazilLeague).Fixtures.All(x => !x.Played));
            Assert.AreEqual(1, s.World.History.Count);
            foreach (var c in s.World.Clubs.Values) Assert.GreaterOrEqual(c.Squad.Count, 17, c.Name);
        }

        [Test]
        public void TrainingGivesXpAndImprovesAttributes()
        {
            var s = NewCareer(CareerOrigin.Academy);
            int before = s.Athlete.Attributes.Finishing;
            for (int week = 0; week < 6; week++)
            {
                s.TrainingSessionsThisWeek = 0;
                CareerManager.ApplyTraining(s, "finishing", 0.9f);
            }
            Assert.Greater(s.Athlete.Attributes.Finishing, before);
            Assert.Greater(s.Level * 1000 + s.Xp, 1000);
            Assert.AreEqual(6, s.Training.Count);
        }

        [Test]
        public void TrainingIsLimitedPerWeek()
        {
            var s = NewCareer();
            CareerManager.ApplyTraining(s, "speed", 0.5f);
            CareerManager.ApplyTraining(s, "speed", 0.5f);
            Assert.IsFalse(CareerManager.CanTrain(s));
        }

        [Test]
        public void LevelUpsUnlockPerksThatChangeGameplayTraits()
        {
            var s = NewCareer();
            CareerManager.GainXp(s, 2000);
            Assert.GreaterOrEqual(s.Level, 3);
            Assert.IsNotEmpty(s.UnlockedPerks);
            Assert.IsTrue(s.UnlockedPerks.All(t => s.Athlete.HasTrait(t)));
        }

        [Test]
        public void AcceptingATransferMovesTheAthlete()
        {
            var s = NewCareer();
            var rng = new Rng(3);
            string oldClub = s.ClubId;
            var target = s.World.Clubs.Values.First(c => c.Id != oldClub);
            s.Offers.Add(new TransferOffer { Id = "of-test", Type = OfferType.Transfer, ClubId = target.Id, Wage = 50, Years = 3, Role = SquadRole.Starter, Fee = 1000, ExpiresDay = 99 });
            long budgetBefore = target.Budget;
            string msg = CareerManager.RespondToOffer(s, "of-test", true);
            StringAssert.Contains(target.Name, msg);
            Assert.AreEqual(target.Id, s.ClubId);
            Assert.Contains(s.AthleteId, target.Squad);
            Assert.IsFalse(s.World.Club(oldClub).Squad.Contains(s.AthleteId));
            Assert.AreEqual(budgetBefore - 1000, target.Budget);
            Assert.AreEqual(2029, s.AthleteState.Contract.UntilSeason);
            Assert.IsTrue(s.News.Any(n => n.Category == NewsCategory.Transfer));
        }

        [Test]
        public void LoanSendsAndReturnsAtSeasonEnd()
        {
            var s = NewCareer(CareerOrigin.PromotedYouth);
            string parent = s.ClubId;
            var target = s.World.Clubs.Values.First(c => c.Id != parent);
            s.Offers.Add(new TransferOffer { Id = "loan", Type = OfferType.Loan, ClubId = target.Id, Wage = 3, Years = 1, Role = SquadRole.Starter, ExpiresDay = 99 });
            CareerManager.RespondToOffer(s, "loan", true);
            Assert.AreEqual(target.Id, s.ClubId);
            Assert.AreEqual(parent, s.AthleteState.Contract.LoanFromClubId);
            CareerManager.EndSeason(s);
            Assert.AreEqual(parent, s.ClubId);
        }

        [Test]
        public void CoachConversationChangesTrust()
        {
            var s = NewCareer();
            s.Trust = 30;
            s.AthleteState.Form = 6.0f;
            float before = s.Trust;
            var answer = CareerManager.TalkToCoach(s, CareerManager.CoachRequest.StartingPlace);
            Assert.Less(s.Trust, before);
            StringAssert.Contains(":", answer);
            var again = CareerManager.TalkToCoach(s, CareerManager.CoachRequest.StartingPlace);
            StringAssert.Contains("semana", again);
        }

        [Test]
        public void SaveAndLoadRoundTripPreservesTheCareer()
        {
            var s = NewCareer();
            var f = CareerManager.AdvanceToNextMatch(s);
            CareerManager.SimulateUserMatch(s, CareerManager.PrepareMatch(s, f));
            var mem = new MemoryFileStore();
            var store = new CareerStore(new SaveSystem(mem));
            store.Save(s);
            Assert.AreEqual(LoadStatus.Ok, store.TryLoad(s.Id, out var loaded, out var err), err);
            Assert.AreEqual(s.Athlete.DisplayName, loaded.Athlete.DisplayName);
            Assert.AreEqual(s.Athlete.Attributes.Finishing, loaded.Athlete.Attributes.Finishing);
            Assert.AreEqual(s.Day, loaded.Day);
            Assert.AreEqual(s.MatchLog.Count, loaded.MatchLog.Count);
            Assert.AreEqual(s.News.Count, loaded.News.Count);
            Assert.AreEqual(s.World.Players.Count, loaded.World.Players.Count);
            Assert.AreEqual(s.World.Competition(WorldBuilder.BrazilLeague).Fixtures.Count(x => x.Played),
                            loaded.World.Competition(WorldBuilder.BrazilLeague).Fixtures.Count(x => x.Played));
            // The loaded career keeps working.
            var f2 = CareerManager.AdvanceToNextMatch(loaded);
            Assert.IsNotNull(f2);
            Assert.AreEqual(1, store.List().Count);
        }

        [Test]
        public void CorruptedSaveRecoversFromBackup()
        {
            var s = NewCareer();
            var mem = new MemoryFileStore();
            var store = new CareerStore(new SaveSystem(mem));
            store.Save(s);
            s.Day = 5;
            store.Save(s); // previous version becomes the .bak
            string file = CareerStore.FileName(s.Id);
            mem.Files[file] = mem.Files[file].Substring(0, mem.Files[file].Length / 2); // truncated write
            var status = store.TryLoad(s.Id, out var loaded, out _);
            Assert.AreEqual(LoadStatus.RecoveredFromBackup, status);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(0, loaded.Day);
        }

        [Test]
        public void TamperedSaveIsDetected()
        {
            var s = NewCareer();
            var mem = new MemoryFileStore();
            var store = new CareerStore(new SaveSystem(mem));
            store.Save(s);
            string file = CareerStore.FileName(s.Id);
            mem.Files[file] = mem.Files[file].Replace("Teste", "Hackd");
            var status = store.TryLoad(s.Id, out var loaded, out var err);
            Assert.AreEqual(LoadStatus.Corrupted, status);
            StringAssert.Contains("checksum", err);
        }

        [Test]
        public void MultipleCareersAreIndependent()
        {
            var mem = new MemoryFileStore();
            var store = new CareerStore(new SaveSystem(mem));
            var a = NewCareer(seed: 1); var b = NewCareer(CareerOrigin.Academy, Position.CB, 2);
            store.Save(a); store.Save(b);
            Assert.AreEqual(2, store.List().Count);
            store.Delete(a.Id);
            var list = store.List();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(b.Id, list[0].Id);
        }

        [Test]
        public void YoungAthleteImprovesOverASeason()
        {
            var s = NewCareer(CareerOrigin.PromotedYouth, seed: 11);
            int start = s.Athlete.Overall;
            int guard = 0;
            while (guard++ < 80)
            {
                var f = CareerManager.AdvanceToNextMatch(s);
                if (f == null) break;
                if (CareerManager.CanTrain(s)) CareerManager.ApplyTraining(s, "finishing", 0.75f);
                CareerManager.SimulateUserMatch(s, CareerManager.PrepareMatch(s, f));
            }
            Assert.Greater(s.Athlete.Overall, start);
        }
    }

    public class SaveSystemTests
    {
        private sealed class V1 { public string Name = "x"; }
        private sealed class V2 { public string FullName; public int Level; }

        [Test]
        public void MigrationsUpgradeOldSaves()
        {
            var mem = new MemoryFileStore();
            var saves = new SaveSystem(mem);
            saves.Save("t.json", "thing", 1, new V1 { Name = "Ana" });
            saves.RegisterMigration("thing", 1, jo => { jo["FullName"] = jo["Name"]; jo["Level"] = 1; return jo; });
            Assert.AreEqual(LoadStatus.Ok, saves.TryLoad<V2>("t.json", "thing", 2, out var v2, out var err), err);
            Assert.AreEqual("Ana", v2.FullName);
            Assert.AreEqual(1, v2.Level);
        }

        [Test]
        public void SavesFromNewerVersionsAreNotLoaded()
        {
            var mem = new MemoryFileStore();
            var saves = new SaveSystem(mem);
            saves.Save("t.json", "thing", 5, new V1());
            Assert.AreEqual(LoadStatus.IncompatibleVersion, saves.TryLoad<V1>("t.json", "thing", 1, out _, out _));
        }

        [Test]
        public void SettingsRoundTripAndValidation()
        {
            var mem = new MemoryFileStore();
            var saves = new SaveSystem(mem);
            var s = new NextXI.Core.Settings.GameSettings();
            s.Audio.Music = 0.25f; s.Match.MinutesPerHalf = 8; s.Controls.ButtonScale = 9f;
            s.Validate();
            Assert.AreEqual(1.5f, s.Controls.ButtonScale);
            saves.Save("settings.json", "settings", 1, s);
            Assert.AreEqual(LoadStatus.Ok, saves.TryLoad<NextXI.Core.Settings.GameSettings>("settings.json", "settings", 1, out var l, out _));
            Assert.AreEqual(0.25f, l.Audio.Music);
            Assert.AreEqual(8, l.Match.MinutesPerHalf);
        }

        [Test]
        public void DiskStoreWritesAtomicallyWithBackup()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nextxi_test_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var store = new DiskFileStore(dir);
                store.WriteAtomic("a.json", "one");
                store.WriteAtomic("a.json", "two");
                Assert.AreEqual("two", store.Read("a.json"));
                Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(dir, "a.json.bak")));
                Assert.AreEqual(1, System.Linq.Enumerable.Count(store.List("a")));
            }
            finally { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
        }
    }
}
