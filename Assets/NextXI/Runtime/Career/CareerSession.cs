using System;
using System.Linq;
using NextXI.Core;
using NextXI.Core.Career;
using NextXI.Core.Match;
using NextXI.Core.Persistence;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>
    /// Holds the active career, autosaves after every meaningful step and launches the 3D
    /// match / training scenes for it.
    /// </summary>
    public static class CareerSession
    {
        public static CareerState Current { get; private set; }
        private static CareerStore _store;
        public static CareerStore Store => _store ?? (_store = new CareerStore(GameApp.Instance.Saves));

        public static void Begin(CareerState s)
        {
            Current = s;
            Save();
        }

        public static void Save()
        {
            if (Current == null) return;
            try { Store.Save(Current); }
            catch (Exception e) { Log.Exception("Career", e); }
        }

        public static LoadStatus Load(string id, out string error)
        {
            var st = Store.TryLoad(id, out var s, out error);
            if (s != null) Current = s;
            return st;
        }

        public static void Close() { Save(); Current = null; }

        /// <summary>Plays the athlete's next match in 3D (or simulates it).</summary>
        public static void PlayMatch(MatchPlan plan, bool simulate, bool watchFromBench = false)
        {
            var s = Current;
            if (simulate || plan.Unavailable || plan.OnBench && !watchFromBench)
            {
                CareerManager.SimulateUserMatch(s, plan);
                Save();
                GameApp.Instance.SetScreen(CareerScreens.Hub(CareerHubScreen.Tab.Home, "Partida simulada."));
                return;
            }
            var gs = GameApp.Instance.Settings;
            var settings = gs.ToMatchSettings(s.Seed ^ (ulong)(s.World.Season * 1000 + s.Day));
            settings.ControlMode = ControlMode.PlayerLock;
            var home = s.World.Club(plan.Fixture.HomeId);
            GameApp.Instance.StartMatch(new MatchSetup
            {
                Home = plan.Home,
                Away = plan.Away,
                Settings = settings,
                UserTeam = plan.UserTeam,
                LockedPlayerProfileId = s.AthleteId,
                // The coach decides now whether (and when) the benched athlete comes on.
                UserSubInMinute = plan.OnBench
                    ? (CareerManager.RngFor(s, 1201).Chance(CareerManager.BenchChance(s)) ? CareerManager.RngFor(s, 1202).Range(55f, 80f) : -1f)
                    : -1f,
                Competition = plan.Competition.Name.ToUpperInvariant(),
                StadiumName = home.Stadium.ToUpperInvariant(),
                Night = plan.Fixture.Day % 3 != 0,
                OnFinished = report =>
                {
                    CareerManager.ApplyUserMatch(s, plan, report);
                    Save();
                    GameApp.Instance.SetScreen(CareerScreens.Hub(CareerHubScreen.Tab.Home, "Resultado registrado na carreira."));
                },
                OnQuit = () => GameApp.Instance.SetScreen(CareerScreens.Hub(CareerHubScreen.Tab.Home, null))
            });
        }
    }
}
