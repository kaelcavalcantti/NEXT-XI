using System;

namespace NextXI.Core.Match
{
    /// <summary>
    /// Pitch geometry. Origin at the centre spot, X along the length, Z across the width.
    /// Team 0 defends the goal at -HalfLength in the first half.
    /// </summary>
    [Serializable]
    public sealed class PitchSpec
    {
        public float Length = 105f;
        public float Width = 68f;
        public float GoalWidth = 7.32f;
        public float GoalHeight = 2.44f;
        public float GoalDepth = 2.0f;
        public float PostRadius = 0.06f;
        public float PenaltyAreaDepth = 16.5f;
        public float PenaltyAreaWidth = 40.32f;
        public float GoalAreaDepth = 5.5f;
        public float GoalAreaWidth = 18.32f;
        public float PenaltySpotDistance = 11f;
        public float CentreCircleRadius = 9.15f;
        public float CornerArcRadius = 1f;

        public float HalfLength => Length * 0.5f;
        public float HalfWidth => Width * 0.5f;
        public float HalfGoalWidth => GoalWidth * 0.5f;

        /// <summary>Laws of the Game: length 90-120 m, width 45-90 m (international: 100-110 x 64-75).</summary>
        public static PitchSpec Create(float length, float width)
        {
            return new PitchSpec
            {
                Length = MathUtil.Clamp(length, 90f, 120f),
                Width = MathUtil.Clamp(width, 45f, 90f)
            };
        }

        /// <summary>X coordinate of the goal line for the side (-1 or +1).</summary>
        public float GoalLineX(int side) => side * HalfLength;

        public Vec3 GoalCentre(int side) => new Vec3(side * HalfLength, 0, 0);

        public bool IsInsidePenaltyArea(Vec3 p, int side)
        {
            return MathF.Abs(p.Z) <= PenaltyAreaWidth * 0.5f &&
                   p.X * side >= HalfLength - PenaltyAreaDepth &&
                   p.X * side <= HalfLength;
        }

        public bool IsInsideGoalArea(Vec3 p, int side)
        {
            return MathF.Abs(p.Z) <= GoalAreaWidth * 0.5f &&
                   p.X * side >= HalfLength - GoalAreaDepth &&
                   p.X * side <= HalfLength;
        }

        public bool IsInsideField(Vec3 p) => MathF.Abs(p.X) <= HalfLength && MathF.Abs(p.Z) <= HalfWidth;

        public Vec3 PenaltySpot(int side) => new Vec3(side * (HalfLength - PenaltySpotDistance), 0, 0);

        public Vec3 ClampToField(Vec3 p, float margin = 0.5f)
        {
            return new Vec3(
                MathUtil.Clamp(p.X, -HalfLength + margin, HalfLength - margin),
                p.Y,
                MathUtil.Clamp(p.Z, -HalfWidth + margin, HalfWidth - margin));
        }
    }
}
