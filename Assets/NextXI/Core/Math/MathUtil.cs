using System;

namespace NextXI.Core
{
    public static class MathUtil
    {
        public const float Pi = MathF.PI;
        public const float TwoPi = MathF.PI * 2f;

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => Math.Abs(b - a) < 1e-9f ? 0f : Clamp01((v - a) / (b - a));

        /// <summary>Map a 0..99 attribute to a range.</summary>
        public static float Attr(int attribute, float atZero, float atMax) => Lerp(atZero, atMax, Clamp(attribute, 0, 99) / 99f);

        public static float WrapAngle(float a)
        {
            while (a > Pi) a -= TwoPi;
            while (a < -Pi) a += TwoPi;
            return a;
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = WrapAngle(target - current);
            if (Math.Abs(delta) <= maxDelta) return target;
            return WrapAngle(current + Math.Sign(delta) * maxDelta);
        }

        public static float SmoothStep(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Logistic squashing to 0..1.</summary>
        public static float Sigmoid(float x) => 1f / (1f + MathF.Exp(-x));

        /// <summary>Closest point parameter t (0..1) of p on segment a-b, using the flat XZ plane.</summary>
        public static float ClosestFlatT(Vec3 a, Vec3 b, Vec3 p)
        {
            Vec3 ab = (b - a).FlatXZ;
            float len2 = ab.SqrLength;
            if (len2 < 1e-6f) return 0f;
            return Clamp01(Vec3.Dot((p - a).FlatXZ, ab) / len2);
        }
    }

    /// <summary>
    /// Small deterministic PRNG (xorshift128+). Same seed => same match, which makes
    /// simulations reproducible in tests and replays.
    /// </summary>
    [Serializable]
    public sealed class Rng
    {
        private ulong _s0, _s1;

        public Rng(ulong seed)
        {
            // SplitMix64 to expand the seed.
            _s0 = SplitMix(ref seed);
            _s1 = SplitMix(ref seed);
            if (_s0 == 0 && _s1 == 0) _s1 = 1;
        }

        private static ulong SplitMix(ref ulong x)
        {
            ulong z = (x += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextULong()
        {
            ulong s1 = _s0, s0 = _s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return _s1 + s0;
        }

        /// <summary>Uniform float in [0,1).</summary>
        public float Next() => (NextULong() >> 40) / (float)(1UL << 24);
        public float Range(float min, float max) => min + (max - min) * Next();
        public int Range(int minInclusive, int maxExclusive) =>
            maxExclusive <= minInclusive ? minInclusive : minInclusive + (int)(NextULong() % (ulong)(maxExclusive - minInclusive));
        public bool Chance(float p) => Next() < p;

        /// <summary>Approximately normal distributed value (mean 0, sd 1).</summary>
        public float Gaussian()
        {
            float u1 = MathF.Max(1e-7f, Next());
            float u2 = Next();
            return MathF.Sqrt(-2f * MathF.Log(u1)) * MathF.Cos(MathUtil.TwoPi * u2);
        }

        /// <summary>Poisson sample (Knuth), fine for small lambdas used by the stat simulator.</summary>
        public int Poisson(float lambda)
        {
            float l = MathF.Exp(-lambda), p = 1f;
            int k = 0;
            do { k++; p *= Next(); } while (p > l && k < 30);
            return k - 1;
        }

        public T Pick<T>(System.Collections.Generic.IReadOnlyList<T> list) => list[Range(0, list.Count)];
    }
}
