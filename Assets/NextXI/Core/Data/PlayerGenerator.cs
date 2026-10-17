using System;
using System.Collections.Generic;
using NextXI.Core.Match;

namespace NextXI.Core.Data
{
    /// <summary>
    /// Deterministic generator of FICTIONAL players for the demo universe and for squads
    /// without licensed data. Names are synthesised from neutral syllables, never copied
    /// from real people.
    /// </summary>
    public static class PlayerGenerator
    {
        private static readonly string[] FirstNames =
        {
            "Aren", "Bento", "Caio", "Dario", "Elian", "Fabio", "Gael", "Heitor", "Ivo", "Joel", "Kauan", "Lior",
            "Mateo", "Nilo", "Otto", "Pietro", "Quim", "Rui", "Saulo", "Teo", "Ulisses", "Vitor", "Wesley", "Yuri",
            "Zeno", "Adel", "Bruno", "Ciro", "Davi", "Enzo", "Felipe", "Gui", "Hugo", "Igor", "Jonas", "Luan",
            "Milo", "Noah", "Oscar", "Pablo", "Raul", "Sami", "Tiago", "Vasco", "Will", "Yago", "Ziggy", "Amaro"
        };

        private static readonly string[] SurnameStarts =
        {
            "Al", "Bar", "Cor", "Dam", "Est", "Fal", "Gar", "Hol", "Iba", "Jar", "Kel", "Lam", "Mor", "Nav", "Ost",
            "Pen", "Quel", "Ros", "Sal", "Tor", "Uriz", "Val", "Wen", "Xar", "Yor", "Zan", "Bel", "Cas", "Dor", "Fer"
        };

        private static readonly string[] SurnameEnds =
        {
            "ves", "eira", "anto", "ini", "ez", "orra", "ano", "elli", "ado", "ara", "osa", "ier", "mann", "ski",
            "son", "ard", "ent", "ova", "eto", "uz", "iani", "ont", "ais", "ero"
        };

        public static readonly string[] Nationalities =
        {
            "Brazil", "Portugal", "Argentina", "Spain", "France", "Italy", "Germany", "England", "Netherlands",
            "Uruguay", "Colombia", "Mexico", "Nigeria", "Senegal", "Japan", "USA", "Belgium", "Croatia", "Ghana", "Chile"
        };

        public static string RandomName(Rng rng, out string first, out string last)
        {
            first = rng.Pick(FirstNames);
            last = rng.Pick(SurnameStarts) + rng.Pick(SurnameEnds);
            return first + " " + last;
        }

        /// <summary>Generates a fictional player around a target overall for a position.</summary>
        public static PlayerProfile Generate(Rng rng, Position pos, int targetOverall, int age, string idPrefix, int index, int seasonYear = 2026)
        {
            RandomName(rng, out var first, out var last);
            var p = new PlayerProfile
            {
                Id = $"{idPrefix}-{index:000}",
                FirstName = first,
                LastName = last,
                Nationality = rng.Pick(Nationalities),
                BirthDate = $"{seasonYear - age}-{rng.Range(1, 13):00}-{rng.Range(1, 28):00}",
                Position = pos,
                PreferredFoot = rng.Chance(0.24f) ? Foot.Left : Foot.Right,
                IsFictional = true,
                DataSource = "NEXT XI generated fictional player"
            };
            var group = PositionInfo.Group(pos);
            p.HeightCm = group == PositionGroup.Goalkeeper ? rng.Range(184, 198) :
                         pos == Position.CB ? rng.Range(180, 196) :
                         group == PositionGroup.Forward ? rng.Range(170, 192) : rng.Range(168, 190);
            float bmi = rng.Range(21.5f, 24.5f);
            p.WeightKg = (int)MathF.Round(bmi * (p.HeightCm / 100f) * (p.HeightCm / 100f));
            p.Attributes = GenerateAttributes(rng, pos, targetOverall, p.HeightCm);
            p.Attributes.WeakFoot = rng.Range(2, 5) + (rng.Chance(0.1f) ? 1 : 0);
            p.Potential = MathUtil.Clamp(targetOverall + (age < 21 ? rng.Range(6, 22) : age < 25 ? rng.Range(2, 10) : rng.Range(0, 3)), targetOverall, 95);
            p.Reputation = MathUtil.Clamp(targetOverall - 30 + rng.Range(-5, 6), 1, 99);
            p.Appearance = RandomAppearance(rng);
            AssignTraits(rng, p);
            return p;
        }

        public static Appearance RandomAppearance(Rng rng)
        {
            var a = new Appearance
            {
                SkinTone = rng.Next(),
                FaceWidth = rng.Range(0.25f, 0.75f),
                FaceLength = rng.Range(0.25f, 0.75f),
                Hair = (HairStyle)rng.Range(0, 8),
                Beard = rng.Chance(0.55f) ? BeardStyle.None : (BeardStyle)rng.Range(1, 5),
                Build = rng.Range(0.3f, 0.7f),
                BootStyle = rng.Range(0, 6)
            };
            a.HairColor = a.SkinTone > 0.55f ? rng.Range(0f, 0.15f) : rng.Range(0f, 1f);
            return a;
        }

        public static PlayerAttributes GenerateAttributes(Rng rng, Position pos, int target, int heightCm)
        {
            var a = new PlayerAttributes();
            int R(int center, int spread = 6) => MathUtil.Clamp(center + rng.Range(-spread, spread + 1), 15, 97);
            int t = target;
            var g = PositionInfo.Group(pos);
            foreach (var name in PlayerAttributes.Names) a.Set(name, R(t - 18, 8));
            float h = (heightCm - 182) / 10f;

            switch (g)
            {
                case PositionGroup.Goalkeeper:
                    a.GkDiving = R(t); a.GkHandling = R(t); a.GkPositioning = R(t); a.GkReflexes = R(t + 1); a.GkKicking = R(t - 6, 8);
                    a.Reactions = R(t - 2); a.Jumping = R(t - 8); a.Pace = R(t - 25, 8);
                    break;
                case PositionGroup.Defender:
                    a.Marking = R(t + 1); a.StandingTackle = R(t + 2); a.SlidingTackle = R(t); a.Interceptions = R(t);
                    a.Strength = R(t - 2 + (int)(h * 4)); a.Heading = R(t - 3 + (int)(h * 5)); a.Jumping = R(t - 5 + (int)(h * 3));
                    a.ShortPassing = R(t - 8); a.LongPassing = R(t - 10); a.Reactions = R(t - 3); a.Composure = R(t - 6);
                    a.Pace = R(pos == Position.CB ? t - 10 : t, 7); a.Acceleration = R(pos == Position.CB ? t - 12 : t - 1, 7);
                    a.Stamina = R(t - 2); a.Aggression = R(t - 2);
                    if (pos != Position.CB) { a.Crossing = R(t - 6); a.Dribbling = R(t - 10); }
                    break;
                case PositionGroup.Midfielder:
                    a.ShortPassing = R(t + 3); a.LongPassing = R(t - 2); a.Vision = R(t); a.BallControl = R(t + 1);
                    a.Dribbling = R(t - 3); a.Stamina = R(t); a.Reactions = R(t - 2); a.Composure = R(t - 3);
                    a.LongShots = R(t - 8); a.Agility = R(t - 4); a.Balance = R(t - 4); a.Pace = R(t - 8, 8); a.Acceleration = R(t - 6, 8);
                    if (pos == Position.CDM) { a.Interceptions = R(t); a.StandingTackle = R(t); a.Marking = R(t - 3); a.Strength = R(t - 3); a.Aggression = R(t - 2); }
                    if (pos == Position.CAM) { a.Vision = R(t + 3); a.Dribbling = R(t + 1); a.Finishing = R(t - 6); a.Curve = R(t - 4); }
                    if (pos == Position.LM || pos == Position.RM) { a.Crossing = R(t); a.Pace = R(t, 6); a.Acceleration = R(t, 6); }
                    break;
                default:
                    a.Finishing = R(t + 3); a.Positioning = R(t + 2); a.ShotPower = R(t); a.BallControl = R(t);
                    a.Dribbling = R(t - 1); a.Composure = R(t - 2); a.Reactions = R(t - 1); a.Pace = R(t - 1, 8); a.Acceleration = R(t, 8);
                    a.Heading = R(t - 8 + (int)(h * 6)); a.Strength = R(t - 8 + (int)(h * 5)); a.LongShots = R(t - 6); a.Agility = R(t - 3);
                    a.Curve = R(t - 6); a.ShortPassing = R(t - 8);
                    if (pos == Position.LW || pos == Position.RW) { a.Dribbling = R(t + 3); a.Pace = R(t + 3, 5); a.Acceleration = R(t + 3, 5); a.Crossing = R(t - 3); a.Agility = R(t + 1); }
                    break;
            }
            if (g != PositionGroup.Goalkeeper)
            {
                a.GkDiving = R(12, 5); a.GkHandling = R(12, 5); a.GkKicking = R(12, 5); a.GkPositioning = R(12, 5); a.GkReflexes = R(12, 5);
            }
            // Calibrate so the position overall lands close to the target.
            for (int iter = 0; iter < 6; iter++)
            {
                int diff = t - a.Overall(pos);
                if (Math.Abs(diff) <= 1) break;
                foreach (var kv in PlayerAttributes.OverallWeights(pos)) a.Set(kv.Key, a.Get(kv.Key) + diff);
            }
            return a;
        }

        private static void AssignTraits(Rng rng, PlayerProfile p)
        {
            var a = p.Attributes;
            void Maybe(string trait, bool cond, float chance) { if (cond && rng.Chance(chance)) p.Traits.Add(trait); }
            Maybe(Traits.FinesseShot, a.Curve > 75 && a.Finishing > 70, 0.5f);
            Maybe(Traits.PowerShot, a.ShotPower > 80, 0.5f);
            Maybe(Traits.CloseControl, a.Dribbling > 78, 0.5f);
            Maybe(Traits.ThroughBallVision, a.Vision > 78, 0.5f);
            Maybe(Traits.Explosive, a.Acceleration > 82, 0.5f);
            Maybe(Traits.AerialThreat, a.Heading > 76 && p.HeightCm > 185, 0.6f);
            Maybe(Traits.CleanTackler, a.StandingTackle > 76, 0.45f);
            Maybe(Traits.Interceptor, a.Interceptions > 77, 0.45f);
            Maybe(Traits.Shielder, a.Strength > 80, 0.4f);
            Maybe(Traits.ShotStopper, a.GkReflexes > 78, 0.5f);
            Maybe(Traits.Sweeper, a.GkPositioning > 76, 0.3f);
            Maybe(Traits.Crosser, a.Crossing > 77, 0.5f);
            Maybe(Traits.Engine, a.Stamina > 82, 0.5f);
        }

        /// <summary>Builds a full squad (11 starters fitting the formation + 9 bench) for a fictional club.</summary>
        public static List<PlayerProfile> GenerateSquad(Rng rng, string clubId, int baseOverall, string formationName = "4-3-3", int seasonYear = 2026)
        {
            var f = Formation.Get(formationName);
            var squad = new List<PlayerProfile>();
            int idx = 0;
            var usedNumbers = new HashSet<int>();
            int Number(Position pos)
            {
                int[] pref;
                switch (pos)
                {
                    case Position.GK: pref = new[] { 1, 12, 23, 31 }; break;
                    case Position.CB: pref = new[] { 4, 3, 5, 15, 25 }; break;
                    case Position.LB: case Position.LWB: pref = new[] { 6, 16, 26 }; break;
                    case Position.RB: case Position.RWB: pref = new[] { 2, 13, 22 }; break;
                    case Position.CDM: pref = new[] { 5, 8, 18 }; break;
                    case Position.CM: pref = new[] { 8, 10, 14, 20 }; break;
                    case Position.CAM: pref = new[] { 10, 20 }; break;
                    case Position.LW: case Position.LM: pref = new[] { 11, 7, 17 }; break;
                    case Position.RW: case Position.RM: pref = new[] { 7, 11, 17 }; break;
                    default: pref = new[] { 9, 19, 21, 29 }; break;
                }
                foreach (var n in pref) if (usedNumbers.Add(n)) return n;
                for (int n = 24; n < 99; n++) if (usedNumbers.Add(n)) return n;
                return 99;
            }
            foreach (var slot in f.Slots)
            {
                var p = Generate(rng, slot.Position, baseOverall + rng.Range(-4, 5), rng.Range(20, 33), clubId, idx++, seasonYear);
                p.ClubId = clubId;
                p.ShirtNumber = Number(p.Position);
                squad.Add(p);
            }
            Position[] benchPositions = { Position.GK, Position.CB, Position.LB, Position.CDM, Position.CM, Position.CAM, Position.RW, Position.ST, Position.LW };
            foreach (var bp in benchPositions)
            {
                int age = rng.Range(18, 34);
                var p = Generate(rng, bp, baseOverall - rng.Range(3, 9) + (age < 21 ? -3 : 0), age, clubId, idx++, seasonYear);
                p.ClubId = clubId;
                p.ShirtNumber = Number(p.Position);
                squad.Add(p);
            }
            return squad;
        }
    }
}
