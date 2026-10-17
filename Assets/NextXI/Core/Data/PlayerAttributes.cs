using System;
using System.Collections.Generic;

namespace NextXI.Core.Data
{
    public enum Position
    {
        GK, CB, LB, RB, LWB, RWB, CDM, CM, CAM, LM, RM, LW, RW, CF, ST
    }

    public enum PositionGroup { Goalkeeper, Defender, Midfielder, Forward }

    public enum Foot { Right, Left }

    public static class PositionInfo
    {
        public static PositionGroup Group(Position p)
        {
            switch (p)
            {
                case Position.GK: return PositionGroup.Goalkeeper;
                case Position.CB: case Position.LB: case Position.RB: case Position.LWB: case Position.RWB: return PositionGroup.Defender;
                case Position.CDM: case Position.CM: case Position.CAM: case Position.LM: case Position.RM: return PositionGroup.Midfielder;
                default: return PositionGroup.Forward;
            }
        }

        public static string ShortName(Position p) => p.ToString();
    }

    /// <summary>
    /// Individual attributes on a 0..99 scale. Every gameplay system reads these values
    /// directly (speed, acceleration, turning, kick accuracy, keeper reactions...).
    /// </summary>
    [Serializable]
    public sealed class PlayerAttributes
    {
        // Physical
        public int Pace = 60, Acceleration = 60, Agility = 60, Balance = 60, Stamina = 60, Strength = 60, Jumping = 60;
        // Technical
        public int BallControl = 60, Dribbling = 60, ShortPassing = 60, LongPassing = 60, Crossing = 60,
                   Finishing = 60, ShotPower = 60, LongShots = 60, Heading = 60, Curve = 60;
        // Mental
        public int Vision = 60, Positioning = 60, Composure = 60, Reactions = 60, Aggression = 60;
        // Defending
        public int Marking = 60, StandingTackle = 60, SlidingTackle = 60, Interceptions = 60;
        // Goalkeeping
        public int GkDiving = 20, GkHandling = 20, GkKicking = 20, GkPositioning = 20, GkReflexes = 20;
        /// <summary>1..5 weak foot rating.</summary>
        public int WeakFoot = 3;

        public static readonly string[] Names =
        {
            nameof(Pace), nameof(Acceleration), nameof(Agility), nameof(Balance), nameof(Stamina), nameof(Strength), nameof(Jumping),
            nameof(BallControl), nameof(Dribbling), nameof(ShortPassing), nameof(LongPassing), nameof(Crossing),
            nameof(Finishing), nameof(ShotPower), nameof(LongShots), nameof(Heading), nameof(Curve),
            nameof(Vision), nameof(Positioning), nameof(Composure), nameof(Reactions), nameof(Aggression),
            nameof(Marking), nameof(StandingTackle), nameof(SlidingTackle), nameof(Interceptions),
            nameof(GkDiving), nameof(GkHandling), nameof(GkKicking), nameof(GkPositioning), nameof(GkReflexes)
        };

        public int Get(string name)
        {
            switch (name)
            {
                case nameof(Pace): return Pace;
                case nameof(Acceleration): return Acceleration;
                case nameof(Agility): return Agility;
                case nameof(Balance): return Balance;
                case nameof(Stamina): return Stamina;
                case nameof(Strength): return Strength;
                case nameof(Jumping): return Jumping;
                case nameof(BallControl): return BallControl;
                case nameof(Dribbling): return Dribbling;
                case nameof(ShortPassing): return ShortPassing;
                case nameof(LongPassing): return LongPassing;
                case nameof(Crossing): return Crossing;
                case nameof(Finishing): return Finishing;
                case nameof(ShotPower): return ShotPower;
                case nameof(LongShots): return LongShots;
                case nameof(Heading): return Heading;
                case nameof(Curve): return Curve;
                case nameof(Vision): return Vision;
                case nameof(Positioning): return Positioning;
                case nameof(Composure): return Composure;
                case nameof(Reactions): return Reactions;
                case nameof(Aggression): return Aggression;
                case nameof(Marking): return Marking;
                case nameof(StandingTackle): return StandingTackle;
                case nameof(SlidingTackle): return SlidingTackle;
                case nameof(Interceptions): return Interceptions;
                case nameof(GkDiving): return GkDiving;
                case nameof(GkHandling): return GkHandling;
                case nameof(GkKicking): return GkKicking;
                case nameof(GkPositioning): return GkPositioning;
                case nameof(GkReflexes): return GkReflexes;
                default: throw new ArgumentException("Unknown attribute " + name);
            }
        }

        public void Set(string name, int value)
        {
            value = MathUtil.Clamp(value, 1, 99);
            switch (name)
            {
                case nameof(Pace): Pace = value; break;
                case nameof(Acceleration): Acceleration = value; break;
                case nameof(Agility): Agility = value; break;
                case nameof(Balance): Balance = value; break;
                case nameof(Stamina): Stamina = value; break;
                case nameof(Strength): Strength = value; break;
                case nameof(Jumping): Jumping = value; break;
                case nameof(BallControl): BallControl = value; break;
                case nameof(Dribbling): Dribbling = value; break;
                case nameof(ShortPassing): ShortPassing = value; break;
                case nameof(LongPassing): LongPassing = value; break;
                case nameof(Crossing): Crossing = value; break;
                case nameof(Finishing): Finishing = value; break;
                case nameof(ShotPower): ShotPower = value; break;
                case nameof(LongShots): LongShots = value; break;
                case nameof(Heading): Heading = value; break;
                case nameof(Curve): Curve = value; break;
                case nameof(Vision): Vision = value; break;
                case nameof(Positioning): Positioning = value; break;
                case nameof(Composure): Composure = value; break;
                case nameof(Reactions): Reactions = value; break;
                case nameof(Aggression): Aggression = value; break;
                case nameof(Marking): Marking = value; break;
                case nameof(StandingTackle): StandingTackle = value; break;
                case nameof(SlidingTackle): SlidingTackle = value; break;
                case nameof(Interceptions): Interceptions = value; break;
                case nameof(GkDiving): GkDiving = value; break;
                case nameof(GkHandling): GkHandling = value; break;
                case nameof(GkKicking): GkKicking = value; break;
                case nameof(GkPositioning): GkPositioning = value; break;
                case nameof(GkReflexes): GkReflexes = value; break;
                default: throw new ArgumentException("Unknown attribute " + name);
            }
        }

        public PlayerAttributes Clone() => (PlayerAttributes)MemberwiseClone();

        /// <summary>Position-weighted overall rating (0..99).</summary>
        public int Overall(Position pos)
        {
            var w = OverallWeights(pos);
            float sum = 0, wsum = 0;
            foreach (var kv in w) { sum += Get(kv.Key) * kv.Value; wsum += kv.Value; }
            return MathUtil.Clamp((int)MathF.Round(sum / wsum), 1, 99);
        }

        public static Dictionary<string, float> OverallWeights(Position pos)
        {
            switch (PositionInfo.Group(pos))
            {
                case PositionGroup.Goalkeeper:
                    return new Dictionary<string, float>
                    {
                        { nameof(GkDiving), 3 }, { nameof(GkHandling), 3 }, { nameof(GkPositioning), 3 },
                        { nameof(GkReflexes), 3.5f }, { nameof(GkKicking), 1 }, { nameof(Reactions), 1.5f }
                    };
                case PositionGroup.Defender:
                    if (pos == Position.CB)
                        return new Dictionary<string, float>
                        {
                            { nameof(Marking), 3 }, { nameof(StandingTackle), 3 }, { nameof(SlidingTackle), 2 },
                            { nameof(Interceptions), 2.5f }, { nameof(Heading), 2 }, { nameof(Strength), 2 },
                            { nameof(Jumping), 1 }, { nameof(Reactions), 1.5f }, { nameof(ShortPassing), 1 }, { nameof(Pace), 1 }
                        };
                    return new Dictionary<string, float>
                    {
                        { nameof(Pace), 2 }, { nameof(Acceleration), 1.5f }, { nameof(Stamina), 1.5f }, { nameof(Crossing), 1.5f },
                        { nameof(Marking), 2 }, { nameof(StandingTackle), 2.5f }, { nameof(SlidingTackle), 2 },
                        { nameof(Interceptions), 2 }, { nameof(ShortPassing), 1.5f }, { nameof(Reactions), 1.5f }
                    };
                case PositionGroup.Midfielder:
                    if (pos == Position.CDM)
                        return new Dictionary<string, float>
                        {
                            { nameof(ShortPassing), 3 }, { nameof(LongPassing), 2 }, { nameof(Interceptions), 3 },
                            { nameof(StandingTackle), 2.5f }, { nameof(Marking), 2 }, { nameof(Stamina), 1.5f },
                            { nameof(Strength), 1.5f }, { nameof(Vision), 1.5f }, { nameof(Reactions), 1.5f }, { nameof(BallControl), 1.5f }
                        };
                    if (pos == Position.CAM)
                        return new Dictionary<string, float>
                        {
                            { nameof(ShortPassing), 3 }, { nameof(Vision), 3 }, { nameof(BallControl), 3 }, { nameof(Dribbling), 2.5f },
                            { nameof(LongShots), 1.5f }, { nameof(Finishing), 1.5f }, { nameof(Agility), 1 }, { nameof(Reactions), 1.5f },
                            { nameof(Composure), 1 }
                        };
                    return new Dictionary<string, float>
                    {
                        { nameof(ShortPassing), 3 }, { nameof(LongPassing), 2 }, { nameof(Vision), 2 }, { nameof(BallControl), 2.5f },
                        { nameof(Dribbling), 1.5f }, { nameof(Stamina), 1.5f }, { nameof(Interceptions), 1 },
                        { nameof(StandingTackle), 1 }, { nameof(Reactions), 1.5f }, { nameof(LongShots), 1 }
                    };
                default:
                    if (pos == Position.LW || pos == Position.RW)
                        return new Dictionary<string, float>
                        {
                            { nameof(Pace), 2.5f }, { nameof(Acceleration), 2.5f }, { nameof(Dribbling), 3 }, { nameof(BallControl), 2.5f },
                            { nameof(Crossing), 1.5f }, { nameof(Finishing), 1.5f }, { nameof(Agility), 1.5f },
                            { nameof(ShortPassing), 1.5f }, { nameof(Reactions), 1 }
                        };
                    return new Dictionary<string, float>
                    {
                        { nameof(Finishing), 3.5f }, { nameof(Positioning), 2.5f }, { nameof(ShotPower), 2 }, { nameof(Heading), 1.5f },
                        { nameof(BallControl), 2 }, { nameof(Pace), 1.5f }, { nameof(Acceleration), 1.5f }, { nameof(Dribbling), 1.5f },
                        { nameof(Composure), 1.5f }, { nameof(Reactions), 1.5f }, { nameof(Strength), 1 }
                    };
            }
        }
    }
}
