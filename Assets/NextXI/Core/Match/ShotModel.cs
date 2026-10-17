using System;

namespace NextXI.Core.Match
{
    /// <summary>
    /// Expected-goals estimate from distance, visible goal angle, blockers and keeper position.
    /// Used by the AI to decide when to shoot and recorded in statistics.
    /// </summary>
    public static class ShotModel
    {
        public static float ExpectedGoals(MatchEngine m, MatchPlayer shooter, Vec3 from, bool header)
        {
            int side = m.AttackDir(shooter.Team);
            var pitch = m.Pitch;
            Vec3 goal = pitch.GoalCentre(side);
            float dist = Vec3.FlatDistance(from, goal);
            if (from.X * side > pitch.HalfLength) return 0.01f;

            // Visible angle between the posts.
            Vec3 p1 = new Vec3(goal.X, 0, -pitch.HalfGoalWidth) - from.FlatXZ;
            Vec3 p2 = new Vec3(goal.X, 0, pitch.HalfGoalWidth) - from.FlatXZ;
            float angle = MathF.Abs(Vec3.SignedFlatAngle(p1, p2));
            float angleFactor = MathUtil.Clamp01(angle / 0.9f);

            float baseXg = 0.95f * MathF.Exp(-dist / 8.5f) * (0.35f + 0.65f * angleFactor);
            if (header) baseXg *= 0.55f;

            // Opponents in the shooting cone.
            float blockers = 1f;
            Vec3 toGoal = (goal - from).FlatXZ;
            foreach (var o in m.Teams[1 - shooter.Team].Players)
            {
                if (!o.IsActive || o.IsGoalkeeper) continue;
                Vec3 rel = (o.Pos - from).FlatXZ;
                float along = Vec3.Dot(rel, toGoal.Normalized);
                if (along < 0.3f || along > dist) continue;
                float perp = (rel - toGoal.Normalized * along).Length;
                float coneHalf = pitch.HalfGoalWidth * along / MathF.Max(1f, dist) + 0.4f;
                if (perp < coneHalf) blockers *= along < 3f ? 0.55f : 0.75f;
            }

            // Keeper well positioned on the shot line reduces xG; out of position increases it.
            float keeper = 1f;
            var gk = m.Teams[1 - shooter.Team].Goalkeeper;
            if (gk != null)
            {
                Vec3 rel = (gk.Pos - from).FlatXZ;
                float along = Vec3.Dot(rel, toGoal.Normalized);
                float perp = (rel - toGoal.Normalized * MathF.Max(0f, along)).Length;
                if (along > 0) keeper = MathUtil.Lerp(0.8f, 1.6f, MathUtil.Clamp01(perp / 4f));
                else keeper = 1.8f;
            }

            int skill = header ? shooter.A.Heading : (dist > 20f ? shooter.A.LongShots : shooter.A.Finishing);
            float skillFactor = 0.55f + 0.9f * skill / 99f;
            return MathUtil.Clamp(baseXg * blockers * keeper * skillFactor, 0.005f, 0.95f);
        }
    }
}
