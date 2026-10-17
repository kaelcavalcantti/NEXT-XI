using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core.Data;

namespace NextXI.Core.Match
{
    /// <summary>Picks the best XI for a formation from a squad (used by AI coaches and quick matches).</summary>
    public static class TeamSheetBuilder
    {
        public static TeamSheet Build(string clubId, string name, string shortName, IList<PlayerProfile> squad, string formationName,
                                      KitColors kit, TeamTactics tactics = null, IEnumerable<string> unavailableIds = null,
                                      string forceStarterId = null, string forceBenchId = null)
        {
            var f = Formation.Get(formationName);
            var excluded = new HashSet<string>(unavailableIds ?? Enumerable.Empty<string>());
            var pool = squad.Where(p => !excluded.Contains(p.Id)).ToList();
            var chosen = new PlayerProfile[f.Slots.Length];
            var used = new HashSet<string>();

            if (forceStarterId != null)
            {
                var fp = pool.FirstOrDefault(p => p.Id == forceStarterId);
                if (fp != null)
                {
                    int bestSlot = -1; float bestFit = float.MinValue;
                    for (int i = 0; i < f.Slots.Length; i++)
                    {
                        float fit = Fit(fp, f.Slots[i].Position);
                        if (fit > bestFit) { bestFit = fit; bestSlot = i; }
                    }
                    chosen[bestSlot] = fp;
                    used.Add(fp.Id);
                }
            }

            // Greedy: fill slots in order of scarcity (GK first, then others).
            var order = Enumerable.Range(0, f.Slots.Length).OrderBy(i => f.Slots[i].Position == Position.GK ? 0 : 1).ToList();
            foreach (int i in order)
            {
                if (chosen[i] != null) continue;
                var slotPos = f.Slots[i].Position;
                PlayerProfile best = null; float bestScore = float.MinValue;
                foreach (var p in pool)
                {
                    if (used.Contains(p.Id) || p.Id == forceBenchId) continue;
                    float s = Fit(p, slotPos);
                    if (s > bestScore) { bestScore = s; best = p; }
                }
                if (best == null)
                    best = pool.FirstOrDefault(p => !used.Contains(p.Id)) ?? throw new InvalidOperationException($"Squad of {name} too small");
                chosen[i] = best;
                used.Add(best.Id);
            }

            var bench = pool.Where(p => !used.Contains(p.Id)).OrderByDescending(p => p.Overall).Take(9).ToList();
            if (forceBenchId != null && bench.All(b => b.Id != forceBenchId))
            {
                var fb = pool.FirstOrDefault(p => p.Id == forceBenchId);
                if (fb != null) { if (bench.Count >= 9) bench.RemoveAt(bench.Count - 1); bench.Insert(0, fb); }
            }
            return new TeamSheet
            {
                ClubId = clubId,
                Name = name,
                ShortName = shortName,
                FormationName = f.Name,
                Kit = kit ?? new KitColors(),
                Tactics = tactics ?? new TeamTactics(),
                StartingXI = chosen.ToList(),
                Bench = bench
            };
        }

        /// <summary>How well a player fits a position (overall at that position, with familiarity bonus).</summary>
        public static float Fit(PlayerProfile p, Position slot)
        {
            bool gkSlot = slot == Position.GK;
            bool gkPlayer = p.Position == Position.GK;
            if (gkSlot != gkPlayer) return -100f;
            float s = p.Attributes.Overall(slot);
            if (p.Position == slot) s += 6f;
            else if (p.CanPlay(slot)) s += 4f;
            else if (PositionInfo.Group(p.Position) == PositionInfo.Group(slot)) s += 0f;
            else s -= 8f;
            return s;
        }
    }
}
