using System;
using System.Collections.Generic;
using NextXI.Core.Data;

namespace NextXI.Core.Match
{
    public enum TacticalRole
    {
        Goalkeeper,
        CentreBack,
        FullBack,
        DefensiveMid,
        CentralMid,
        AttackingMid,
        WideMid,
        Winger,
        Striker
    }

    /// <summary>
    /// A slot of a formation. Depth: 0 = defensive line, 1 = forward line.
    /// Lateral: -1 = left touchline, +1 = right touchline (seen attacking).
    /// </summary>
    [Serializable]
    public struct FormationSlot
    {
        public Position Position;
        public TacticalRole Role;
        public float Depth;
        public float Lateral;

        public FormationSlot(Position p, TacticalRole r, float depth, float lateral)
        {
            Position = p; Role = r; Depth = depth; Lateral = lateral;
        }
    }

    [Serializable]
    public sealed class Formation
    {
        public string Name;
        public FormationSlot[] Slots;

        public static readonly Dictionary<string, Formation> All = new Dictionary<string, Formation>();

        static Formation()
        {
            Register(new Formation
            {
                Name = "4-3-3",
                Slots = new[]
                {
                    new FormationSlot(Position.GK, TacticalRole.Goalkeeper, -0.4f, 0),
                    new FormationSlot(Position.LB, TacticalRole.FullBack, 0.02f, -0.78f),
                    new FormationSlot(Position.CB, TacticalRole.CentreBack, 0f, -0.28f),
                    new FormationSlot(Position.CB, TacticalRole.CentreBack, 0f, 0.28f),
                    new FormationSlot(Position.RB, TacticalRole.FullBack, 0.02f, 0.78f),
                    new FormationSlot(Position.CDM, TacticalRole.DefensiveMid, 0.3f, 0f),
                    new FormationSlot(Position.CM, TacticalRole.CentralMid, 0.5f, -0.38f),
                    new FormationSlot(Position.CM, TacticalRole.CentralMid, 0.5f, 0.38f),
                    new FormationSlot(Position.LW, TacticalRole.Winger, 0.85f, -0.75f),
                    new FormationSlot(Position.ST, TacticalRole.Striker, 1f, 0f),
                    new FormationSlot(Position.RW, TacticalRole.Winger, 0.85f, 0.75f),
                }
            });
            Register(new Formation
            {
                Name = "4-4-2",
                Slots = new[]
                {
                    new FormationSlot(Position.GK, TacticalRole.Goalkeeper, -0.4f, 0),
                    new FormationSlot(Position.LB, TacticalRole.FullBack, 0.02f, -0.78f),
                    new FormationSlot(Position.CB, TacticalRole.CentreBack, 0f, -0.28f),
                    new FormationSlot(Position.CB, TacticalRole.CentreBack, 0f, 0.28f),
                    new FormationSlot(Position.RB, TacticalRole.FullBack, 0.02f, 0.78f),
                    new FormationSlot(Position.LM, TacticalRole.WideMid, 0.48f, -0.75f),
                    new FormationSlot(Position.CM, TacticalRole.CentralMid, 0.42f, -0.24f),
                    new FormationSlot(Position.CM, TacticalRole.CentralMid, 0.42f, 0.24f),
                    new FormationSlot(Position.RM, TacticalRole.WideMid, 0.48f, 0.75f),
                    new FormationSlot(Position.ST, TacticalRole.Striker, 0.95f, -0.2f),
                    new FormationSlot(Position.ST, TacticalRole.Striker, 0.95f, 0.2f),
                }
            });
            Register(new Formation
            {
                Name = "4-2-3-1",
                Slots = new[]
                {
                    new FormationSlot(Position.GK, TacticalRole.Goalkeeper, -0.4f, 0),
                    new FormationSlot(Position.LB, TacticalRole.FullBack, 0.02f, -0.78f),
                    new FormationSlot(Position.CB, TacticalRole.CentreBack, 0f, -0.28f),
                    new FormationSlot(Position.CB, TacticalRole.CentreBack, 0f, 0.28f),
                    new FormationSlot(Position.RB, TacticalRole.FullBack, 0.02f, 0.78f),
                    new FormationSlot(Position.CDM, TacticalRole.DefensiveMid, 0.3f, -0.22f),
                    new FormationSlot(Position.CDM, TacticalRole.DefensiveMid, 0.3f, 0.22f),
                    new FormationSlot(Position.LM, TacticalRole.Winger, 0.72f, -0.72f),
                    new FormationSlot(Position.CAM, TacticalRole.AttackingMid, 0.68f, 0f),
                    new FormationSlot(Position.RM, TacticalRole.Winger, 0.72f, 0.72f),
                    new FormationSlot(Position.ST, TacticalRole.Striker, 1f, 0f),
                }
            });
            Register(new Formation
            {
                Name = "3-5-2",
                Slots = new[]
                {
                    new FormationSlot(Position.GK, TacticalRole.Goalkeeper, -0.4f, 0),
                    new FormationSlot(Position.CB, TacticalRole.CentreBack, 0f, -0.42f),
                    new FormationSlot(Position.CB, TacticalRole.CentreBack, -0.03f, 0f),
                    new FormationSlot(Position.CB, TacticalRole.CentreBack, 0f, 0.42f),
                    new FormationSlot(Position.LWB, TacticalRole.FullBack, 0.38f, -0.82f),
                    new FormationSlot(Position.CDM, TacticalRole.DefensiveMid, 0.3f, 0f),
                    new FormationSlot(Position.CM, TacticalRole.CentralMid, 0.5f, -0.34f),
                    new FormationSlot(Position.CM, TacticalRole.CentralMid, 0.5f, 0.34f),
                    new FormationSlot(Position.RWB, TacticalRole.FullBack, 0.38f, 0.82f),
                    new FormationSlot(Position.ST, TacticalRole.Striker, 0.95f, -0.2f),
                    new FormationSlot(Position.ST, TacticalRole.Striker, 0.95f, 0.2f),
                }
            });
        }

        private static void Register(Formation f) => All[f.Name] = f;

        public static Formation Get(string name) => name != null && All.TryGetValue(name, out var f) ? f : All["4-3-3"];
    }
}
