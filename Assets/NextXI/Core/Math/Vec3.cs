using System;

namespace NextXI.Core
{
    /// <summary>
    /// Engine-independent 3D vector used by the simulation core.
    /// Coordinate convention: X = pitch length (goal to goal), Y = up, Z = pitch width.
    /// Matches Unity's left-handed Y-up convention so conversion is a field copy.
    /// </summary>
    [Serializable]
    public struct Vec3 : IEquatable<Vec3>
    {
        public float X, Y, Z;

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);
        public static readonly Vec3 Up = new Vec3(0, 1, 0);
        public static readonly Vec3 Right = new Vec3(1, 0, 0);
        public static readonly Vec3 Forward = new Vec3(0, 0, 1);

        public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static Vec3 Flat(float x, float z) => new Vec3(x, 0, z);

        public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);
        public float SqrLength => X * X + Y * Y + Z * Z;
        public float FlatLength => MathF.Sqrt(X * X + Z * Z);
        public Vec3 FlatXZ => new Vec3(X, 0, Z);

        public Vec3 Normalized
        {
            get
            {
                float l = Length;
                return l > 1e-6f ? new Vec3(X / l, Y / l, Z / l) : Zero;
            }
        }

        public Vec3 FlatNormalized
        {
            get
            {
                float l = FlatLength;
                return l > 1e-6f ? new Vec3(X / l, 0, Z / l) : Zero;
            }
        }

        public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(float s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static Vec3 Cross(Vec3 a, Vec3 b) =>
            new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static float Distance(Vec3 a, Vec3 b) => (a - b).Length;
        public static float FlatDistance(Vec3 a, Vec3 b) => (a - b).FlatLength;
        public static Vec3 Lerp(Vec3 a, Vec3 b, float t) => a + (b - a) * t;

        /// <summary>Returns the vector with its length clamped to max.</summary>
        public Vec3 ClampLength(float max)
        {
            float l = Length;
            return l > max && l > 1e-6f ? this * (max / l) : this;
        }

        /// <summary>Rotate around the Y axis (radians, positive = counter-clockwise seen from above with X right, Z up-screen).</summary>
        public Vec3 RotateY(float radians)
        {
            float c = MathF.Cos(radians), s = MathF.Sin(radians);
            return new Vec3(X * c - Z * s, Y, X * s + Z * c);
        }

        /// <summary>Signed angle between two flat directions (radians, -PI..PI).</summary>
        public static float SignedFlatAngle(Vec3 from, Vec3 to)
        {
            float a = MathF.Atan2(to.Z, to.X) - MathF.Atan2(from.Z, from.X);
            return MathUtil.WrapAngle(a);
        }

        public static Vec3 FromAngle(float radians) => new Vec3(MathF.Cos(radians), 0, MathF.Sin(radians));
        public float FlatAngle => MathF.Atan2(Z, X);

        public bool Equals(Vec3 o) => X == o.X && Y == o.Y && Z == o.Z;
        public override bool Equals(object obj) => obj is Vec3 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y, Z);
        public override string ToString() => $"({X:0.00}, {Y:0.00}, {Z:0.00})";
    }
}
