using System;
using System.Collections.Generic;
using System.Linq;
using NextXI.Core.Data;

namespace NextXI.Core.Career
{
    /// <summary>
    /// NEXT XI player archetypes (original design). Each archetype has natural positions,
    /// attribute priorities (where XP goes), a starting attribute profile and a ladder of
    /// perks. Perks are engine Traits, so they change real gameplay (e.g. Explosive gives a
    /// stronger knock-on burst, ShotStopper widens keeper reach).
    /// </summary>
    public sealed class Archetype
    {
        public string Id;
        public string Name;
        public string Description;
        public Position[] Positions;
        /// <summary>Attribute weights for growth (sum is normalised).</summary>
        public Dictionary<string, float> Growth;
        /// <summary>Starting deltas applied on creation (relative to a neutral profile).</summary>
        public Dictionary<string, int> Bias;
        /// <summary>Perks unlocked at levels 2, 4, 6, 8, 10 (two options each from level 6: specialisation).</summary>
        public string[][] PerkLadder;

        private static Dictionary<string, float> G(params (string, float)[] w) => w.ToDictionary(x => x.Item1, x => x.Item2);
        private static Dictionary<string, int> B(params (string, int)[] w) => w.ToDictionary(x => x.Item1, x => x.Item2);

        public static readonly List<Archetype> All = new List<Archetype>
        {
            new Archetype { Id = "speed-winger", Name = "Ponta Veloz", Description = "Explosão, profundidade e cruzamentos.", Positions = new[] { Position.RW, Position.LW, Position.RM, Position.LM },
                Growth = G(("Pace", 3), ("Acceleration", 3), ("Dribbling", 2.5f), ("Crossing", 2), ("Agility", 1.5f), ("Finishing", 1), ("BallControl", 1.5f)),
                Bias = B(("Pace", 10), ("Acceleration", 10), ("Dribbling", 5), ("Crossing", 4), ("Strength", -6), ("Heading", -6)),
                PerkLadder = new[] { new[] { Traits.Explosive }, new[] { Traits.Crosser }, new[] { Traits.CloseControl, Traits.FinesseShot }, new[] { Traits.Engine }, new[] { Traits.FirstTimePass } } },
            new Archetype { Id = "creative-dribbler", Name = "Driblador Criativo", Description = "Controle curto, dribles e chutes colocados.", Positions = new[] { Position.CAM, Position.LW, Position.RW },
                Growth = G(("Dribbling", 3), ("BallControl", 3), ("Agility", 2), ("Balance", 2), ("Vision", 1.5f), ("Curve", 1.5f), ("Finishing", 1)),
                Bias = B(("Dribbling", 9), ("BallControl", 8), ("Agility", 7), ("Balance", 6), ("Strength", -8), ("Marking", -8)),
                PerkLadder = new[] { new[] { Traits.CloseControl }, new[] { Traits.FinesseShot }, new[] { Traits.Explosive, Traits.ThroughBallVision }, new[] { Traits.FirstTimePass }, new[] { Traits.Shielder } } },
            new Archetype { Id = "poacher", Name = "Centroavante Finalizador", Description = "Posicionamento na área e finalização.", Positions = new[] { Position.ST, Position.CF },
                Growth = G(("Finishing", 3.5f), ("Positioning", 3), ("Composure", 2), ("Reactions", 2), ("ShotPower", 1.5f), ("Heading", 1)),
                Bias = B(("Finishing", 10), ("Positioning", 9), ("Composure", 5), ("Stamina", -5), ("Marking", -10)),
                PerkLadder = new[] { new[] { Traits.FinesseShot }, new[] { Traits.FirstTimePass }, new[] { Traits.PowerShot, Traits.AerialThreat }, new[] { Traits.CloseControl }, new[] { Traits.Explosive } } },
            new Archetype { Id = "target-forward", Name = "Atacante Físico", Description = "Força, jogo aéreo e pivô.", Positions = new[] { Position.ST, Position.CF },
                Growth = G(("Strength", 3), ("Heading", 3), ("Jumping", 2), ("ShotPower", 2), ("Finishing", 2), ("BallControl", 1)),
                Bias = B(("Strength", 10), ("Heading", 10), ("Jumping", 7), ("ShotPower", 5), ("Agility", -8), ("Acceleration", -5)),
                PerkLadder = new[] { new[] { Traits.AerialThreat }, new[] { Traits.Shielder }, new[] { Traits.PowerShot, Traits.FirstTimePass }, new[] { Traits.Engine }, new[] { Traits.FinesseShot } } },
            new Archetype { Id = "playmaker", Name = "Meia Armador", Description = "Visão, passe e ritmo.", Positions = new[] { Position.CM, Position.CAM },
                Growth = G(("Vision", 3.5f), ("ShortPassing", 3), ("LongPassing", 2.5f), ("BallControl", 2), ("Curve", 1.5f), ("Composure", 1.5f)),
                Bias = B(("Vision", 10), ("ShortPassing", 9), ("LongPassing", 7), ("Pace", -6), ("Strength", -5)),
                PerkLadder = new[] { new[] { Traits.ThroughBallVision }, new[] { Traits.FirstTimePass }, new[] { Traits.FinesseShot, Traits.Crosser }, new[] { Traits.CloseControl }, new[] { Traits.Engine } } },
            new Archetype { Id = "box-to-box", Name = "Box-to-Box", Description = "Fôlego, chegada à área e desarmes.", Positions = new[] { Position.CM },
                Growth = G(("Stamina", 3), ("ShortPassing", 2), ("StandingTackle", 2), ("Strength", 1.5f), ("LongShots", 1.5f), ("Positioning", 1.5f), ("Interceptions", 1.5f)),
                Bias = B(("Stamina", 10), ("Strength", 5), ("StandingTackle", 5), ("LongShots", 4)),
                PerkLadder = new[] { new[] { Traits.Engine }, new[] { Traits.PowerShot }, new[] { Traits.Interceptor, Traits.ThroughBallVision }, new[] { Traits.CleanTackler }, new[] { Traits.Shielder } } },
            new Archetype { Id = "anchor", Name = "Volante Marcador", Description = "Leitura defensiva, interceptações e proteção.", Positions = new[] { Position.CDM, Position.CM },
                Growth = G(("Interceptions", 3), ("StandingTackle", 3), ("Marking", 2.5f), ("Strength", 2), ("ShortPassing", 1.5f), ("Aggression", 1)),
                Bias = B(("Interceptions", 10), ("StandingTackle", 9), ("Marking", 7), ("Strength", 5), ("Finishing", -10), ("Dribbling", -5)),
                PerkLadder = new[] { new[] { Traits.Interceptor }, new[] { Traits.CleanTackler }, new[] { Traits.Shielder, Traits.Engine }, new[] { Traits.ThroughBallVision }, new[] { Traits.AerialThreat } } },
            new Archetype { Id = "ball-playing-cb", Name = "Zagueiro Construtor", Description = "Saída de bola e antecipação.", Positions = new[] { Position.CB },
                Growth = G(("ShortPassing", 2.5f), ("LongPassing", 2.5f), ("Interceptions", 2.5f), ("Composure", 2), ("Marking", 2), ("StandingTackle", 2)),
                Bias = B(("ShortPassing", 9), ("LongPassing", 8), ("Composure", 6), ("Interceptions", 6), ("Strength", -3), ("Heading", -3)),
                PerkLadder = new[] { new[] { Traits.Interceptor }, new[] { Traits.ThroughBallVision }, new[] { Traits.CleanTackler, Traits.FirstTimePass }, new[] { Traits.Shielder }, new[] { Traits.Engine } } },
            new Archetype { Id = "stopper-cb", Name = "Zagueiro Dominante", Description = "Força, jogo aéreo e duelos.", Positions = new[] { Position.CB },
                Growth = G(("Strength", 3), ("Heading", 3), ("StandingTackle", 2.5f), ("Marking", 2.5f), ("Jumping", 2), ("Aggression", 1)),
                Bias = B(("Strength", 10), ("Heading", 10), ("Jumping", 6), ("StandingTackle", 6), ("Pace", -5), ("ShortPassing", -4)),
                PerkLadder = new[] { new[] { Traits.AerialThreat }, new[] { Traits.CleanTackler }, new[] { Traits.Shielder, Traits.Interceptor }, new[] { Traits.Engine }, new[] { Traits.PowerShot } } },
            new Archetype { Id = "attacking-fullback", Name = "Lateral Ofensivo", Description = "Apoio, ultrapassagens e cruzamentos.", Positions = new[] { Position.RB, Position.LB, Position.RWB, Position.LWB },
                Growth = G(("Crossing", 3), ("Stamina", 2.5f), ("Pace", 2), ("Acceleration", 2), ("Dribbling", 1.5f), ("StandingTackle", 1.5f)),
                Bias = B(("Crossing", 9), ("Stamina", 8), ("Pace", 6), ("Acceleration", 6), ("Marking", -5)),
                PerkLadder = new[] { new[] { Traits.Crosser }, new[] { Traits.Engine }, new[] { Traits.Explosive, Traits.ThroughBallVision }, new[] { Traits.CleanTackler }, new[] { Traits.CloseControl } } },
            new Archetype { Id = "defensive-fullback", Name = "Lateral Defensivo", Description = "Marcação, cobertura e solidez.", Positions = new[] { Position.RB, Position.LB },
                Growth = G(("Marking", 3), ("StandingTackle", 3), ("SlidingTackle", 2.5f), ("Interceptions", 2), ("Strength", 1.5f), ("Pace", 1.5f)),
                Bias = B(("Marking", 9), ("StandingTackle", 9), ("SlidingTackle", 7), ("Crossing", -5), ("Dribbling", -5)),
                PerkLadder = new[] { new[] { Traits.CleanTackler }, new[] { Traits.Interceptor }, new[] { Traits.Engine, Traits.Shielder }, new[] { Traits.AerialThreat }, new[] { Traits.Crosser } } },
            new Archetype { Id = "agile-keeper", Name = "Goleiro Ágil", Description = "Reflexos e mergulhos espetaculares.", Positions = new[] { Position.GK },
                Growth = G(("GkReflexes", 3.5f), ("GkDiving", 3), ("Reactions", 2), ("GkHandling", 1.5f), ("Jumping", 1)),
                Bias = B(("GkReflexes", 9), ("GkDiving", 8), ("Reactions", 5), ("GkPositioning", -4)),
                PerkLadder = new[] { new[] { Traits.ShotStopper }, new[] { Traits.Sweeper }, new[] { Traits.Engine }, new[] { Traits.FirstTimePass }, new[] { Traits.Interceptor } } },
            new Archetype { Id = "positional-keeper", Name = "Goleiro de Posicionamento", Description = "Colocação, segurança e saída.", Positions = new[] { Position.GK },
                Growth = G(("GkPositioning", 3.5f), ("GkHandling", 3), ("GkKicking", 2), ("Composure", 1.5f), ("GkDiving", 1.5f)),
                Bias = B(("GkPositioning", 9), ("GkHandling", 8), ("GkKicking", 6), ("GkReflexes", -4)),
                PerkLadder = new[] { new[] { Traits.Sweeper }, new[] { Traits.ShotStopper }, new[] { Traits.ThroughBallVision }, new[] { Traits.Engine }, new[] { Traits.Interceptor } } },
        };

        public static Archetype Get(string id) => All.FirstOrDefault(a => a.Id == id) ?? All[0];

        public static IEnumerable<Archetype> ForPosition(Position p) => All.Where(a => a.Positions.Contains(p));
    }

    public enum CareerOrigin
    {
        /// <summary>Academy: 17 years old, low overall, high potential, starts in the youth squad.</summary>
        Academy,
        /// <summary>Promoted youngster: 18, rotation role in the first team.</summary>
        PromotedYouth,
        /// <summary>Established professional: 22, starter-level overall.</summary>
        Professional,
        /// <summary>Take control of an existing registered athlete (demo catalogue).</summary>
        ExistingAthlete
    }
}
