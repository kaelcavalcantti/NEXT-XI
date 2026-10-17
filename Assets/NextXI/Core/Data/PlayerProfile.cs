using System;
using System.Collections.Generic;

namespace NextXI.Core.Data
{
    public enum HairStyle { Buzz, Short, Crop, Medium, Curly, Afro, Bun, Bald }
    public enum BeardStyle { None, Stubble, Short, Full, Goatee }

    /// <summary>
    /// Appearance parameters. All values are bounded so that no combination produces
    /// anatomically implausible bodies (see Clamp()).
    /// </summary>
    [Serializable]
    public sealed class Appearance
    {
        /// <summary>0 = lightest, 1 = darkest (interpolated over a curated palette).</summary>
        public float SkinTone = 0.4f;
        /// <summary>0 = narrow, 1 = wide jaw/cheeks.</summary>
        public float FaceWidth = 0.5f;
        /// <summary>0 = short face, 1 = long face.</summary>
        public float FaceLength = 0.5f;
        public HairStyle Hair = HairStyle.Short;
        /// <summary>0..1 index into the natural hair color palette.</summary>
        public float HairColor = 0.15f;
        public BeardStyle Beard = BeardStyle.None;
        /// <summary>0 = lean, 1 = heavy build (muscle/fat distribution).</summary>
        public float Build = 0.5f;
        public int BootStyle;
        public bool LongSleeves;
        public bool Headband;
        public bool Gloves;

        public void Clamp()
        {
            SkinTone = MathUtil.Clamp01(SkinTone);
            FaceWidth = MathUtil.Clamp01(FaceWidth);
            FaceLength = MathUtil.Clamp01(FaceLength);
            HairColor = MathUtil.Clamp01(HairColor);
            Build = MathUtil.Clamp01(Build);
            BootStyle = MathUtil.Clamp(BootStyle, 0, 5);
        }

        public Appearance Clone() => (Appearance)MemberwiseClone();
    }

    /// <summary>
    /// Persistent catalogue record of a footballer (real-licensed, fictional or the user's athlete).
    /// </summary>
    [Serializable]
    public sealed class PlayerProfile
    {
        public string Id;
        public string FirstName;
        public string LastName;
        public string KnownAs;
        public string Nationality;
        /// <summary>ISO yyyy-MM-dd.</summary>
        public string BirthDate;
        public int HeightCm = 180;
        public int WeightKg = 75;
        public Position Position = Position.CM;
        public List<Position> SecondaryPositions = new List<Position>();
        public Foot PreferredFoot = Foot.Right;
        public PlayerAttributes Attributes = new PlayerAttributes();
        public Appearance Appearance = new Appearance();
        public string ClubId;
        public int ShirtNumber;
        /// <summary>True for generated/demo players. Real players require DataSource licensing info.</summary>
        public bool IsFictional = true;
        public string DataSource = "NEXT XI demo universe (fictional)";
        /// <summary>0..99 hidden potential.</summary>
        public int Potential = 70;
        public int Reputation = 30;
        public List<string> Traits = new List<string>();

        public string DisplayName => !string.IsNullOrEmpty(KnownAs) ? KnownAs : $"{FirstName} {LastName}".Trim();
        public string ShortName => !string.IsNullOrEmpty(KnownAs) ? KnownAs : (string.IsNullOrEmpty(FirstName) ? LastName : $"{FirstName[0]}. {LastName}");
        public int Overall => Attributes.Overall(Position);

        public int AgeOn(DateTime date)
        {
            if (!DateTime.TryParse(BirthDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var bd)) return 25;
            int age = date.Year - bd.Year;
            if (date < bd.AddYears(age)) age--;
            return age;
        }

        public bool CanPlay(Position p) => Position == p || (SecondaryPositions != null && SecondaryPositions.Contains(p));

        public bool HasTrait(string trait) => Traits != null && Traits.Contains(trait);
    }

    /// <summary>Names of gameplay traits/skills that alter behaviour in the match engine.</summary>
    public static class Traits
    {
        /// <summary>Finesse shots curl more and are more accurate to the far corner.</summary>
        public const string FinesseShot = "FinesseShot";
        /// <summary>Power shots: higher speed at long range, less error penalty.</summary>
        public const string PowerShot = "PowerShot";
        /// <summary>Faster close-control touches, smaller touch distance while dribbling.</summary>
        public const string CloseControl = "CloseControl";
        /// <summary>First-time passes lose less accuracy.</summary>
        public const string FirstTimePass = "FirstTimePass";
        /// <summary>Through balls are weighted better (less lead error).</summary>
        public const string ThroughBallVision = "ThroughBallVision";
        /// <summary>Burst acceleration after a knock-on (skill move).</summary>
        public const string Explosive = "Explosive";
        /// <summary>Wins more aerial duels, longer header reach.</summary>
        public const string AerialThreat = "AerialThreat";
        /// <summary>Clean tackles: lower foul chance, higher success.</summary>
        public const string CleanTackler = "CleanTackler";
        /// <summary>Intercepts at longer reach and reads passes earlier.</summary>
        public const string Interceptor = "Interceptor";
        /// <summary>Hard to dispossess while shielding.</summary>
        public const string Shielder = "Shielder";
        /// <summary>Goalkeeper: faster dives and bigger reach.</summary>
        public const string ShotStopper = "ShotStopper";
        /// <summary>Goalkeeper: comes for crosses more often and claims them.</summary>
        public const string Sweeper = "Sweeper";
        /// <summary>Crosses curl more accurately into the box.</summary>
        public const string Crosser = "Crosser";
        /// <summary>Stamina drains slower.</summary>
        public const string Engine = "Engine";
    }
}
