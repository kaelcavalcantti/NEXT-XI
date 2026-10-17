using System;

namespace NextXI.Runtime.AudioSynth
{
    /// <summary>
    /// Original procedural sound design for NEXT XI (no recordings or third-party samples).
    /// Pure C#: every generator returns mono float samples in -1..1 at the given rate, so the
    /// output can be verified offline (tools/NextXI.AudioPreview) and loaded into AudioClips.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 22050;

        private sealed class Rnd
        {
            private uint _s;
            public Rnd(uint seed) { _s = seed == 0 ? 1u : seed; }
            public float Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return (_s & 0xFFFFFF) / (float)0x1000000; }
            public float Bi() => Next() * 2f - 1f;
        }

        private static float[] Buf(float seconds) => new float[Math.Max(1, (int)(seconds * Rate))];

        /// <summary>One-pole low-pass in place.</summary>
        private static void LowPass(float[] x, float cutoff)
        {
            float a = 1f - MathF.Exp(-2f * MathF.PI * cutoff / Rate), y = 0f;
            for (int i = 0; i < x.Length; i++) { y += a * (x[i] - y); x[i] = y; }
        }

        private static void HighPass(float[] x, float cutoff)
        {
            float a = MathF.Exp(-2f * MathF.PI * cutoff / Rate), y = 0f, prev = 0f;
            for (int i = 0; i < x.Length; i++) { y = a * (y + x[i] - prev); prev = x[i]; x[i] = y; }
        }

        /// <summary>Two-pole resonant band-pass (state variable filter) applied in place.</summary>
        private static void BandPass(float[] x, float freq, float q)
        {
            float f = 2f * MathF.Sin(MathF.PI * MathF.Min(freq, Rate * 0.2f) / Rate);
            float low = 0, band = 0, damp = 1f / q;
            for (int i = 0; i < x.Length; i++)
            {
                low += f * band;
                float high = x[i] - low - damp * band;
                band += f * high;
                x[i] = band;
            }
        }

        public static void Normalize(float[] x, float peak = 0.9f)
        {
            float m = 1e-6f;
            foreach (var v in x) m = MathF.Max(m, MathF.Abs(v));
            float k = peak / m;
            for (int i = 0; i < x.Length; i++) x[i] *= k;
        }

        /// <summary>Crossfades the end into the start so the buffer loops seamlessly.</summary>
        public static float[] MakeLoop(float[] x, float fadeSeconds)
        {
            int f = Math.Min(x.Length / 3, (int)(fadeSeconds * Rate));
            var y = new float[x.Length - f];
            Array.Copy(x, f, y, 0, y.Length);
            for (int i = 0; i < f; i++)
            {
                float t = i / (float)f;
                y[y.Length - f + i] = y[y.Length - f + i] * MathF.Sqrt(1 - t) + x[i] * MathF.Sqrt(t);
            }
            return y;
        }

        // ------------------------------------------------------------------ crowd

        /// <summary>
        /// Stadium bed: thousands of voices approximated by band-limited noise with slowly
        /// wandering vocal formants and murmur swells.
        /// </summary>
        public static float[] CrowdBed(float seconds, uint seed, float energy)
        {
            var r = new Rnd(seed);
            var x = Buf(seconds);
            for (int i = 0; i < x.Length; i++) x[i] = r.Bi();
            var a = (float[])x.Clone();
            var b = (float[])x.Clone();
            BandPass(a, 420f, 1.6f);
            BandPass(b, 1100f, 2.2f);
            LowPass(x, 2600f);
            var outp = Buf(seconds);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float swell = 0.75f + 0.25f * MathF.Sin(t * 0.7f + seed) * MathF.Sin(t * 0.23f);
                outp[i] = (x[i] * 0.35f + a[i] * 0.9f + b[i] * 0.45f * energy) * swell;
            }
            HighPass(outp, 90f);
            Normalize(outp, 0.7f);
            return MakeLoop(outp, 1.0f);
        }

        /// <summary>Goal roar: fast swell of the crowd with a bright "ohh" formant, long decay.</summary>
        public static float[] GoalRoar(uint seed)
        {
            var r = new Rnd(seed);
            var x = Buf(6f);
            for (int i = 0; i < x.Length; i++) x[i] = r.Bi();
            var v1 = (float[])x.Clone(); var v2 = (float[])x.Clone();
            BandPass(v1, 520f, 2.5f);
            BandPass(v2, 900f, 3f);
            LowPass(x, 4000f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float env = t < 0.35f ? t / 0.35f : MathF.Exp(-(t - 0.35f) * 0.45f);
                x[i] = (x[i] * 0.5f + v1[i] * 1.2f + v2[i] * 0.8f) * env;
            }
            HighPass(x, 70f);
            Normalize(x, 0.9f);
            return x;
        }

        /// <summary>Disappointed "ohh" after a near miss: rises then falls in pitch/brightness.</summary>
        public static float[] NearMiss(uint seed)
        {
            var r = new Rnd(seed);
            var x = Buf(2.2f);
            var y = Buf(2.2f);
            for (int i = 0; i < x.Length; i++) x[i] = r.Bi();
            // Sweep a formant from 700 to 380 Hz.
            float low = 0, band = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)x.Length;
                float freq = 700f - 320f * t;
                float f = 2f * MathF.Sin(MathF.PI * freq / Rate);
                low += f * band;
                float high = x[i] - low - 0.4f * band;
                band += f * high;
                float env = t < 0.15f ? t / 0.15f : MathF.Exp(-(t - 0.15f) * 2.2f);
                y[i] = band * env;
            }
            Normalize(y, 0.8f);
            return y;
        }

        /// <summary>Booing: low dark formant noise with tremolo.</summary>
        public static float[] Boo(uint seed)
        {
            var r = new Rnd(seed);
            var x = Buf(2.5f);
            for (int i = 0; i < x.Length; i++) x[i] = r.Bi();
            BandPass(x, 260f, 3f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float env = MathF.Min(1f, t / 0.3f) * MathF.Min(1f, (2.5f - t) / 0.6f);
                x[i] *= env * (0.8f + 0.2f * MathF.Sin(t * 30f));
            }
            Normalize(x, 0.7f);
            return x;
        }

        /// <summary>Applause: many random hand claps (short filtered noise bursts).</summary>
        public static float[] Applause(float seconds, uint seed, float density = 900f)
        {
            var r = new Rnd(seed);
            var x = Buf(seconds);
            int claps = (int)(density * seconds);
            for (int c = 0; c < claps; c++)
            {
                int start = (int)(r.Next() * (x.Length - 400));
                float amp = 0.3f + 0.7f * r.Next();
                float decay = 250f + 300f * r.Next();
                for (int i = 0; i < 380; i++) x[start + i] += r.Bi() * amp * MathF.Exp(-i / (decay * 0.12f));
            }
            BandPass(x, 1800f, 0.8f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                x[i] *= MathF.Min(1f, t / 0.2f) * MathF.Min(1f, (seconds - t) / 0.8f);
            }
            Normalize(x, 0.7f);
            return x;
        }

        /// <summary>
        /// Original terrace chant: rhythmic massed claps (tempo 120) with a sung two-note vowel
        /// line ("o-le") made of detuned voices through vowel formants.
        /// </summary>
        public static float[] Chant(float seconds, uint seed, float baseHz = 196f)
        {
            var r = new Rnd(seed);
            var x = Buf(seconds);
            float beat = 0.5f;
            // Claps on beats 1 and 3, triplet push on 4.
            for (float t = 0; t < seconds - 0.1f; t += beat * 2)
            {
                foreach (float off in new[] { 0f, beat, beat * 1.5f })
                {
                    int s0 = (int)((t + off) * Rate);
                    for (int v = 0; v < 40; v++)
                    {
                        int s = s0 + (int)(r.Next() * 0.03f * Rate);
                        for (int i = 0; i < 300 && s + i < x.Length; i++) x[s + i] += r.Bi() * 0.25f * MathF.Exp(-i / 40f);
                    }
                }
            }
            // Sung line: notes (semitones) over 2-bar pattern.
            int[] melody = { 0, 0, 3, 0, 5, 3, 0, -2 };
            var voice = Buf(seconds);
            for (int n = 0; n * beat < seconds; n++)
            {
                float f0 = baseHz * MathF.Pow(2f, melody[n % melody.Length] / 12f);
                int s0 = (int)(n * beat * Rate), len = (int)(beat * 0.95f * Rate);
                for (int v = 0; v < 6; v++)
                {
                    float det = 1f + (r.Next() - 0.5f) * 0.02f;
                    float ph = r.Next();
                    for (int i = 0; i < len && s0 + i < voice.Length; i++)
                    {
                        float t = i / (float)Rate;
                        ph += f0 * det / Rate;
                        float saw = 2f * (ph - MathF.Floor(ph)) - 1f;
                        float env = MathF.Min(1f, t / 0.04f) * MathF.Min(1f, (len - i) / (0.08f * Rate));
                        voice[s0 + i] += saw * env * 0.15f;
                    }
                }
            }
            var f1 = (float[])voice.Clone(); var f2 = (float[])voice.Clone();
            BandPass(f1, 700f, 4f);
            BandPass(f2, 1150f, 5f);
            for (int i = 0; i < x.Length; i++) x[i] = x[i] * 0.8f + f1[i] * 1.6f + f2[i] * 0.9f;
            LowPass(x, 5000f);
            Normalize(x, 0.75f);
            return x;
        }

        // ------------------------------------------------------------------ match sfx

        /// <summary>Ball strike: low thump (bladder) + leather slap. `power` 0..1 shapes both.</summary>
        public static float[] Kick(float power, uint seed)
        {
            var r = new Rnd(seed);
            var x = Buf(0.25f);
            float f = 95f + 50f * power;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float thump = MathF.Sin(2 * MathF.PI * f * t * (1f - t * 1.5f)) * MathF.Exp(-t * 32f);
                float slap = r.Bi() * MathF.Exp(-t * (180f - 60f * power));
                x[i] = thump * (0.6f + 0.4f * power) + slap * (0.25f + 0.45f * power);
            }
            LowPass(x, 2500f + 3000f * power);
            Normalize(x, 0.5f + 0.45f * power);
            return x;
        }

        /// <summary>Referee whistle: pea-whistle trill (two close partials with fast warble).</summary>
        public static float[] Whistle(float seconds, uint seed)
        {
            var r = new Rnd(seed);
            var x = Buf(seconds);
            float ph1 = 0, ph2 = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float warble = 1f + 0.035f * MathF.Sin(2 * MathF.PI * 34f * t);
                ph1 += 2750f * warble / Rate;
                ph2 += 2890f * warble / Rate;
                float env = MathF.Min(1f, t / 0.015f) * MathF.Min(1f, (seconds - t) / 0.04f);
                x[i] = (MathF.Sin(2 * MathF.PI * ph1) * 0.6f + MathF.Sin(2 * MathF.PI * ph2) * 0.4f + r.Bi() * 0.05f) * env;
            }
            Normalize(x, 0.6f);
            return x;
        }

        /// <summary>Full-time style whistle sequence: short, short, long.</summary>
        public static float[] WhistleTriple(uint seed)
        {
            var a = Whistle(0.22f, seed);
            var c = Whistle(0.9f, seed + 1);
            var x = Buf(0.22f * 2 + 0.9f + 0.4f);
            Array.Copy(a, 0, x, 0, a.Length);
            Array.Copy(a, 0, x, (int)(0.38f * Rate), a.Length);
            Array.Copy(c, 0, x, (int)(0.76f * Rate), Math.Min(c.Length, x.Length - (int)(0.76f * Rate)));
            return x;
        }

        /// <summary>Ball hitting the net: soft swoosh with rustle.</summary>
        public static float[] Net(uint seed)
        {
            var r = new Rnd(seed);
            var x = Buf(0.7f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                x[i] = r.Bi() * MathF.Exp(-t * 6f) * (0.7f + 0.3f * MathF.Sin(t * 90f));
            }
            BandPass(x, 1600f, 0.9f);
            Normalize(x, 0.6f);
            return x;
        }

        /// <summary>Woodwork: metallic inharmonic ring.</summary>
        public static float[] Post(uint seed)
        {
            var x = Buf(1.2f);
            float[] partials = { 520f, 1347f, 2210f, 3105f };
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float s = 0;
                for (int p = 0; p < partials.Length; p++) s += MathF.Sin(2 * MathF.PI * partials[p] * t) * MathF.Exp(-t * (3f + p * 2.5f)) / (p + 1);
                x[i] = s;
            }
            Normalize(x, 0.7f);
            return x;
        }

        /// <summary>Body contact / tackle: dull thud with cloth scuff.</summary>
        public static float[] Tackle(uint seed)
        {
            var r = new Rnd(seed);
            var x = Buf(0.3f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                x[i] = MathF.Sin(2 * MathF.PI * 70f * t) * MathF.Exp(-t * 25f) + r.Bi() * 0.3f * MathF.Exp(-t * 18f);
            }
            LowPass(x, 1200f);
            Normalize(x, 0.6f);
            return x;
        }

        // ------------------------------------------------------------------ interface & music

        public static float[] UiClick(float pitch)
        {
            var x = Buf(0.06f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                x[i] = MathF.Sin(2 * MathF.PI * 1800f * pitch * t) * MathF.Exp(-t * 90f);
            }
            Normalize(x, 0.4f);
            return x;
        }

        public static float[] UiConfirm()
        {
            var x = Buf(0.22f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                float f = t < 0.08f ? 880f : 1318.5f;
                x[i] = (MathF.Sin(2 * MathF.PI * f * t) + 0.3f * MathF.Sin(4 * MathF.PI * f * t)) * MathF.Exp(-((t < 0.08f ? t : t - 0.08f)) * 22f);
            }
            Normalize(x, 0.4f);
            return x;
        }

        /// <summary>
        /// Original menu theme: 16-bar loop in A minor at 112 BPM (pads, pulsing bass,
        /// four-on-the-floor kick, off-beat hats, arpeggiated lead). Seamless loop.
        /// </summary>
        public static float[] MenuMusic(uint seed)
        {
            const float bpm = 112f;
            float beat = 60f / bpm;
            int bars = 8;
            float seconds = bars * 4 * beat;
            var x = Buf(seconds + 0.01f);
            var r = new Rnd(seed);
            // Chord roots (semitones from A2 = 110 Hz): Am - F - C - G
            int[][] chords = { new[] { 0, 3, 7 }, new[] { -4, 0, 3 }, new[] { 3, 7, 10 }, new[] { -2, 2, 5 } };
            int[] roots = { 0, -4, 3, -2 };
            float Hz(int semi, float baseHz = 110f) => baseHz * MathF.Pow(2f, semi / 12f);
            for (int i = 0; i < x.Length; i++)
            {
                float t = i / (float)Rate;
                int beatIdx = (int)(t / beat);
                int bar = beatIdx / 4;
                int chord = bar % 4;
                float inBeat = t - beatIdx * beat;
                float s = 0;
                // Pad (detuned saws, low-passed later by gentle averaging)
                foreach (int n in chords[chord])
                {
                    float f = Hz(n + 12);
                    s += (Saw(f * 1.003f, t) + Saw(f * 0.997f, t)) * 0.035f;
                }
                // Bass: eighth-note pulse
                float eighth = beat * 0.5f;
                float inEighth = t % eighth;
                s += Saw(Hz(roots[chord] - 12 + 12), t) * 0.16f * MathF.Exp(-inEighth * 9f);
                // Kick
                s += MathF.Sin(2 * MathF.PI * (55f + 90f * MathF.Exp(-inBeat * 40f)) * inBeat) * MathF.Exp(-inBeat * 9f) * 0.5f;
                // Off-beat hat
                float hatT = (t + beat * 0.5f) % beat;
                s += r.Bi() * MathF.Exp(-hatT * 60f) * 0.06f;
                // Arpeggio lead (16ths) in the second half
                if (bar >= 4)
                {
                    float six = beat * 0.25f;
                    int step = (int)(t / six);
                    int note = chords[chord][step % 3] + 24 + (step % 6 >= 3 ? 12 : 0);
                    float tn = t % six;
                    s += Square(Hz(note), t) * 0.04f * MathF.Exp(-tn * 14f);
                }
                x[i] = s;
            }
            LowPass(x, 6000f);
            Normalize(x, 0.7f);
            return x;
        }

        private static float Saw(float f, float t) { float p = f * t; return 2f * (p - MathF.Floor(p)) - 1f; }
        private static float Square(float f, float t) => (f * t) % 1f < 0.5f ? 1f : -1f;

        /// <summary>Peak and RMS of a buffer (for offline validation).</summary>
        public static (float peak, float rms, bool finite) Analyze(float[] x)
        {
            float peak = 0, sum = 0; bool finite = true;
            foreach (var v in x) { if (!float.IsFinite(v)) finite = false; peak = MathF.Max(peak, MathF.Abs(v)); sum += v * v; }
            return (peak, MathF.Sqrt(sum / Math.Max(1, x.Length)), finite);
        }
    }
}
