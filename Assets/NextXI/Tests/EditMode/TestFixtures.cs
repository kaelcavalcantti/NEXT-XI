using NextXI.Core;
using NextXI.Core.Data;
using NextXI.Core.Match;

namespace NextXI.Tests
{
    internal static class TestFixtures
    {
        public static TeamSheet Team(ulong seed, string id, int overall = 72, string formation = "4-3-3")
        {
            var rng = new Rng(seed);
            var squad = PlayerGenerator.GenerateSquad(rng, id, overall, formation);
            return TeamSheetBuilder.Build(id, "Team " + id, id, squad, formation, new KitColors());
        }

        public static MatchEngine Engine(int minutes = 3, ulong seed = 1, int homeOverall = 72, int awayOverall = 72)
        {
            var settings = new MatchSettings { RealMinutesPerHalf = minutes, Seed = seed };
            return new MatchEngine(Team(seed * 7 + 1, "H", homeOverall), Team(seed * 7 + 2, "A", awayOverall, "4-4-2"), settings);
        }

        /// <summary>Moves every player far from the action and switches the AI off.</summary>
        public static void ClearPitch(MatchEngine m)
        {
            m.AiEnabled = false;
            int i = 0;
            foreach (var p in m.Players)
            {
                m.PlacePlayer(p, new Vec3(-30f + (i % 11) * 6f, 0, p.Team == 0 ? -30f : 30f), 0f);
                i++;
            }
        }

        public static void Run(MatchEngine m, float seconds)
        {
            int steps = (int)(seconds / MatchEngine.Dt);
            for (int i = 0; i < steps && !m.IsFinished; i++) m.Step();
        }
    }
}
