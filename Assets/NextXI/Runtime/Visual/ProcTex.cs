using System;
using System.Collections.Generic;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>
    /// Procedural textures generated at runtime (no third-party art): turf, nets, ball panels,
    /// crowd cards, LED boards and kit numbers. Text on textures uses an original stroke font.
    /// </summary>
    public static class ProcTex
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        private static Texture2D New(int w, int h, bool alpha, bool mip = true, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            var t = new Texture2D(w, h, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, mip)
            {
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4
            };
            return t;
        }

        private static Texture2D Cached(string key, Func<Texture2D> make)
        {
            if (Cache.TryGetValue(key, out var t) && t != null) return t;
            t = make();
            t.name = "NX_" + key;
            Cache[key] = t;
            return t;
        }

        // ------------------------------------------------------------------ noise

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        private static float ValueNoise(float x, float y, int seed, int period)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int Wrap(int v) => ((v % period) + period) % period;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Tileable fractal noise in 0..1.</summary>
        public static float Fbm(float u, float v, int seed, int baseFreq = 8, int octaves = 4)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            int f = baseFreq;
            for (int o = 0; o < octaves; o++)
            {
                sum += ValueNoise(u * f, v * f, seed + o * 17, f) * amp;
                norm += amp;
                amp *= 0.5f;
                f *= 2;
            }
            return sum / norm;
        }

        // ------------------------------------------------------------------ turf

        /// <summary>One tile = two mowing stripes (light then dark) along U, with blade noise.</summary>
        public static Texture2D Grass(int size = 512) => Cached("grass" + size, () =>
        {
            var t = New(size, size, false);
            var px = new Color32[size * size];
            var light = new Color(0.27f, 0.52f, 0.20f);
            var dark = new Color(0.22f, 0.44f, 0.17f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float stripe = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Mathf.Abs(u - 0.5f) - 0.245f) / 0.01f));
                    var baseC = Color.Lerp(dark, light, stripe);
                    float n = Fbm(u, v, 3, 16, 4);
                    float blades = Fbm(u * 4f % 1f, v * 4f % 1f, 9, 32, 2);
                    float k = 0.86f + n * 0.22f + (blades - 0.5f) * 0.08f;
                    px[y * size + x] = (Color32)(baseC * k);
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        });

        /// <summary>Tileable net mesh pattern (alpha).</summary>
        public static Texture2D Net() => Cached("net", () =>
        {
            const int s = 128;
            var t = New(s, s, true);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    // Diamond (hexagonal-like) mesh.
                    float u = x / (float)s, v = y / (float)s;
                    float d1 = Mathf.Abs(((u + v) * 2f) % 1f - 0.5f);
                    float d2 = Mathf.Abs(((u - v + 1f) * 2f) % 1f - 0.5f);
                    float line = Mathf.Max(Mathf.Clamp01((d1 - 0.44f) / 0.04f), Mathf.Clamp01((d2 - 0.44f) / 0.04f));
                    byte a = (byte)(line * 255);
                    px[y * s + x] = new Color32(240, 240, 240, a);
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        });

        /// <summary>Ball panels: original NEXT XI design (white with navy/orange swooshes).</summary>
        public static Texture2D Ball() => Cached("ball", () =>
        {
            const int w = 512, h = 256;
            var t = New(w, h, false);
            var px = new Color32[w * h];
            var white = new Color(0.96f, 0.96f, 0.95f);
            var navy = new Color(0.07f, 0.11f, 0.27f);
            var orange = new Color(1f, 0.42f, 0.1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)w * Mathf.PI * 2f, v = y / (float)h * Mathf.PI;
                    // Three curved bands wrapping the sphere.
                    float band = Mathf.Sin(u * 3f + Mathf.Sin(v * 2f) * 1.2f);
                    var c = white;
                    if (band > 0.82f) c = navy;
                    else if (band > 0.7f) c = orange;
                    // Panel seams.
                    float seam = Mathf.Abs(Mathf.Sin(u * 6f + v * 1.5f));
                    if (seam < 0.03f) c *= 0.8f;
                    px[y * w + x] = (Color32)c;
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        });

        /// <summary>
        /// Crowd card texture: 2 frames stacked vertically (bottom = seated, top = celebrating),
        /// each with a row of small spectators in varied shirt colours.
        /// </summary>
        public static Texture2D Crowd(Color homeColor, Color awayColor, int seed) => Cached($"crowd{ColorUtility.ToHtmlStringRGB(homeColor)}{seed}", () =>
        {
            const int w = 512, h = 128;
            var t = New(w, h, true, true);
            var px = new Color32[w * h];
            var rng = new System.Random(seed);
            const int people = 24;
            for (int frame = 0; frame < 2; frame++)
            {
                for (int i = 0; i < people; i++)
                {
                    float cx = (i + 0.5f) / people * w + (float)(rng.NextDouble() - 0.5) * 6f;
                    float r = (float)rng.NextDouble();
                    Color shirt = r < 0.55 ? homeColor : r < 0.7 ? awayColor : Color.HSVToRGB((float)rng.NextDouble(), 0.4f, 0.4f + (float)rng.NextDouble() * 0.5f);
                    shirt *= 0.75f + (float)rng.NextDouble() * 0.3f;
                    float skinT = (float)rng.NextDouble();
                    Color skin = Color.Lerp(new Color(0.95f, 0.8f, 0.68f), new Color(0.35f, 0.22f, 0.15f), skinT);
                    Color hair = Color.Lerp(new Color(0.1f, 0.07f, 0.05f), new Color(0.6f, 0.45f, 0.25f), (float)rng.NextDouble() * (1 - skinT));
                    int baseY = frame * 64;
                    float lift = frame == 1 ? 6f + (float)rng.NextDouble() * 4f : (float)rng.NextDouble() * 2f;
                    for (int y = 0; y < 64; y++)
                        for (int x = Mathf.Max(0, (int)cx - 12); x < Mathf.Min(w, (int)cx + 12); x++)
                        {
                            float dx = x - cx, dy = y - (16 + lift);
                            Color? c = null;
                            // Torso (rounded box)
                            if (Mathf.Abs(dx) < 7.5f && dy > -16 && dy < 10) c = shirt;
                            // Head
                            float hy = y - (32 + lift);
                            if (dx * dx + hy * hy < 30f) c = hy > 2.5f ? hair : skin;
                            // Arms raised when celebrating
                            if (frame == 1 && Mathf.Abs(Mathf.Abs(dx) - 9f) < 2f && dy > 4 && dy < 26) c = skin;
                            if (c.HasValue)
                            {
                                float shade = 0.85f + 0.15f * (y / 64f);
                                var cc = c.Value * shade; cc.a = 1f;
                                px[(baseY + y) * w + x] = (Color32)cc;
                            }
                        }
                }
            }
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.Apply(true, true);
            return t;
        });

        /// <summary>Seat texture for empty stands.</summary>
        public static Texture2D Seats(Color c) => Cached("seats" + ColorUtility.ToHtmlStringRGB(c), () =>
        {
            const int s = 64;
            var t = New(s, s, false);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    bool seat = (x % 16) > 2 && (y % 32) > 6 && (y % 32) < 26;
                    px[y * s + x] = (Color32)(seat ? c : c * 0.45f);
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        });

        // ------------------------------------------------------------------ stroke font

        /// <summary>
        /// Original block stroke font (polylines in a 1 x 1.6 box). Used for kit numbers,
        /// LED boards and in-world signage without depending on font rasterisation.
        /// </summary>
        private static readonly Dictionary<char, float[][]> Glyphs = new Dictionary<char, float[][]>
        {
            ['0'] = new[] { new[] { 0.5f, 0f, 0.95f, 0.25f, 0.95f, 1.35f, 0.5f, 1.6f, 0.05f, 1.35f, 0.05f, 0.25f, 0.5f, 0f } },
            ['1'] = new[] { new[] { 0.2f, 1.25f, 0.6f, 1.6f, 0.6f, 0f }, new[] { 0.2f, 0f, 0.95f, 0f } },
            ['2'] = new[] { new[] { 0.05f, 1.3f, 0.35f, 1.6f, 0.75f, 1.6f, 0.95f, 1.3f, 0.95f, 1.0f, 0.05f, 0f, 0.95f, 0f } },
            ['3'] = new[] { new[] { 0.05f, 1.6f, 0.95f, 1.6f, 0.45f, 0.95f, 0.8f, 0.85f, 0.95f, 0.5f, 0.75f, 0.05f, 0.3f, 0f, 0.05f, 0.2f } },
            ['4'] = new[] { new[] { 0.7f, 0f, 0.7f, 1.6f, 0.05f, 0.5f, 0.95f, 0.5f } },
            ['5'] = new[] { new[] { 0.95f, 1.6f, 0.1f, 1.6f, 0.05f, 0.9f, 0.6f, 0.95f, 0.95f, 0.6f, 0.9f, 0.2f, 0.55f, 0f, 0.05f, 0.1f } },
            ['6'] = new[] { new[] { 0.85f, 1.6f, 0.35f, 1.4f, 0.05f, 0.75f, 0.1f, 0.2f, 0.5f, 0f, 0.9f, 0.2f, 0.95f, 0.6f, 0.55f, 0.85f, 0.05f, 0.7f } },
            ['7'] = new[] { new[] { 0.05f, 1.6f, 0.95f, 1.6f, 0.35f, 0f } },
            ['8'] = new[] { new[] { 0.5f, 0.85f, 0.15f, 1.05f, 0.15f, 1.45f, 0.5f, 1.6f, 0.85f, 1.45f, 0.85f, 1.05f, 0.5f, 0.85f, 0.05f, 0.6f, 0.05f, 0.2f, 0.5f, 0f, 0.95f, 0.2f, 0.95f, 0.6f, 0.5f, 0.85f } },
            ['9'] = new[] { new[] { 0.15f, 0f, 0.65f, 0.2f, 0.95f, 0.85f, 0.9f, 1.4f, 0.5f, 1.6f, 0.1f, 1.4f, 0.05f, 1.0f, 0.45f, 0.75f, 0.95f, 0.9f } },
            ['A'] = new[] { new[] { 0.05f, 0f, 0.5f, 1.6f, 0.95f, 0f }, new[] { 0.22f, 0.6f, 0.78f, 0.6f } },
            ['B'] = new[] { new[] { 0.08f, 0f, 0.08f, 1.6f, 0.7f, 1.6f, 0.9f, 1.4f, 0.9f, 1.0f, 0.7f, 0.82f, 0.08f, 0.82f }, new[] { 0.7f, 0.82f, 0.95f, 0.6f, 0.95f, 0.2f, 0.72f, 0f, 0.08f, 0f } },
            ['C'] = new[] { new[] { 0.95f, 1.35f, 0.7f, 1.6f, 0.3f, 1.6f, 0.05f, 1.3f, 0.05f, 0.3f, 0.3f, 0f, 0.7f, 0f, 0.95f, 0.25f } },
            ['D'] = new[] { new[] { 0.08f, 0f, 0.08f, 1.6f, 0.6f, 1.6f, 0.95f, 1.2f, 0.95f, 0.4f, 0.6f, 0f, 0.08f, 0f } },
            ['E'] = new[] { new[] { 0.95f, 1.6f, 0.08f, 1.6f, 0.08f, 0f, 0.95f, 0f }, new[] { 0.08f, 0.82f, 0.75f, 0.82f } },
            ['F'] = new[] { new[] { 0.95f, 1.6f, 0.08f, 1.6f, 0.08f, 0f }, new[] { 0.08f, 0.82f, 0.75f, 0.82f } },
            ['G'] = new[] { new[] { 0.95f, 1.35f, 0.7f, 1.6f, 0.3f, 1.6f, 0.05f, 1.3f, 0.05f, 0.3f, 0.3f, 0f, 0.7f, 0f, 0.95f, 0.3f, 0.95f, 0.75f, 0.55f, 0.75f } },
            ['H'] = new[] { new[] { 0.08f, 0f, 0.08f, 1.6f }, new[] { 0.92f, 0f, 0.92f, 1.6f }, new[] { 0.08f, 0.82f, 0.92f, 0.82f } },
            ['I'] = new[] { new[] { 0.5f, 0f, 0.5f, 1.6f }, new[] { 0.2f, 0f, 0.8f, 0f }, new[] { 0.2f, 1.6f, 0.8f, 1.6f } },
            ['J'] = new[] { new[] { 0.9f, 1.6f, 0.9f, 0.35f, 0.6f, 0f, 0.3f, 0f, 0.05f, 0.3f } },
            ['K'] = new[] { new[] { 0.08f, 0f, 0.08f, 1.6f }, new[] { 0.95f, 1.6f, 0.08f, 0.7f }, new[] { 0.35f, 0.95f, 0.95f, 0f } },
            ['L'] = new[] { new[] { 0.08f, 1.6f, 0.08f, 0f, 0.95f, 0f } },
            ['M'] = new[] { new[] { 0.05f, 0f, 0.05f, 1.6f, 0.5f, 0.7f, 0.95f, 1.6f, 0.95f, 0f } },
            ['N'] = new[] { new[] { 0.08f, 0f, 0.08f, 1.6f, 0.92f, 0f, 0.92f, 1.6f } },
            ['O'] = new[] { new[] { 0.3f, 0f, 0.7f, 0f, 0.95f, 0.3f, 0.95f, 1.3f, 0.7f, 1.6f, 0.3f, 1.6f, 0.05f, 1.3f, 0.05f, 0.3f, 0.3f, 0f } },
            ['P'] = new[] { new[] { 0.08f, 0f, 0.08f, 1.6f, 0.7f, 1.6f, 0.95f, 1.35f, 0.95f, 1.0f, 0.7f, 0.75f, 0.08f, 0.75f } },
            ['Q'] = new[] { new[] { 0.3f, 0f, 0.7f, 0f, 0.95f, 0.3f, 0.95f, 1.3f, 0.7f, 1.6f, 0.3f, 1.6f, 0.05f, 1.3f, 0.05f, 0.3f, 0.3f, 0f }, new[] { 0.6f, 0.35f, 1f, -0.1f } },
            ['R'] = new[] { new[] { 0.08f, 0f, 0.08f, 1.6f, 0.7f, 1.6f, 0.95f, 1.35f, 0.95f, 1.0f, 0.7f, 0.75f, 0.08f, 0.75f }, new[] { 0.5f, 0.75f, 0.95f, 0f } },
            ['S'] = new[] { new[] { 0.95f, 1.35f, 0.7f, 1.6f, 0.3f, 1.6f, 0.05f, 1.35f, 0.05f, 1.05f, 0.3f, 0.85f, 0.7f, 0.75f, 0.95f, 0.55f, 0.95f, 0.25f, 0.7f, 0f, 0.3f, 0f, 0.05f, 0.25f } },
            ['T'] = new[] { new[] { 0.02f, 1.6f, 0.98f, 1.6f }, new[] { 0.5f, 1.6f, 0.5f, 0f } },
            ['U'] = new[] { new[] { 0.08f, 1.6f, 0.08f, 0.3f, 0.3f, 0f, 0.7f, 0f, 0.92f, 0.3f, 0.92f, 1.6f } },
            ['V'] = new[] { new[] { 0.05f, 1.6f, 0.5f, 0f, 0.95f, 1.6f } },
            ['W'] = new[] { new[] { 0.02f, 1.6f, 0.25f, 0f, 0.5f, 0.9f, 0.75f, 0f, 0.98f, 1.6f } },
            ['X'] = new[] { new[] { 0.05f, 1.6f, 0.95f, 0f }, new[] { 0.95f, 1.6f, 0.05f, 0f } },
            ['Y'] = new[] { new[] { 0.05f, 1.6f, 0.5f, 0.8f, 0.95f, 1.6f }, new[] { 0.5f, 0.8f, 0.5f, 0f } },
            ['Z'] = new[] { new[] { 0.05f, 1.6f, 0.95f, 1.6f, 0.05f, 0f, 0.95f, 0f } },
            ['-'] = new[] { new[] { 0.2f, 0.8f, 0.8f, 0.8f } },
            ['.'] = new[] { new[] { 0.45f, 0.02f, 0.55f, 0.02f } },
        };

        /// <summary>Paints `text` into `px` (w x h) using the stroke font. Returns the drawn width in px.</summary>
        public static float DrawText(Color32[] px, int w, int h, string text, float x, float y, float glyphH, float thickness, Color color, float spacing = 0.28f, float italic = 0f)
        {
            float gw = glyphH / 1.6f;
            float cx = x;
            foreach (char ch0 in text.ToUpperInvariant())
            {
                if (ch0 == ' ') { cx += gw * 0.7f; continue; }
                if (!Glyphs.TryGetValue(ch0, out var strokes)) { cx += gw; continue; }
                foreach (var poly in strokes)
                    for (int i = 0; i + 3 < poly.Length; i += 2)
                    {
                        var a = new Vector2(cx + poly[i] * gw + poly[i + 1] * gw * italic, y + poly[i + 1] * gw);
                        var b = new Vector2(cx + poly[i + 2] * gw + poly[i + 3] * gw * italic, y + poly[i + 3] * gw);
                        DrawSegment(px, w, h, a, b, thickness, color);
                    }
                cx += gw * (1f + spacing);
            }
            return cx - x;
        }

        public static float MeasureText(string text, float glyphH, float spacing = 0.28f)
        {
            float gw = glyphH / 1.6f, wsum = 0;
            foreach (char ch in text) wsum += ch == ' ' ? gw * 0.7f : gw * (1f + spacing);
            return wsum;
        }

        private static void DrawSegment(Color32[] px, int w, int h, Vector2 a, Vector2 b, float thick, Color color)
        {
            float r = thick * 0.5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - r - 1));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + r + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - r - 1));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + r + 1));
            Vector2 ab = b - a;
            float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t);
                    float cov = Mathf.Clamp01(r - d + 0.5f);
                    if (cov <= 0) continue;
                    int idx = y * w + x;
                    Color dst = px[idx];
                    Color src = color;
                    float aOut = cov * src.a + dst.a * (1 - cov * src.a);
                    Color outC = aOut > 0 ? (src * (cov * src.a) + dst * (dst.a * (1 - cov * src.a))) / aOut : Color.clear;
                    outC.a = aOut;
                    px[idx] = (Color32)outC;
                }
        }

        /// <summary>Kit number decal (transparent background, outlined digits).</summary>
        public static Texture2D Number(int number, Color fill, Color outline) =>
            Cached($"num{number}_{ColorUtility.ToHtmlStringRGBA(fill)}_{ColorUtility.ToHtmlStringRGBA(outline)}", () =>
            {
                const int w = 128, h = 128;
                var t = New(w, h, true, true, TextureWrapMode.Clamp);
                var px = new Color32[w * h];
                string s = Mathf.Clamp(number, 0, 99).ToString();
                float glyphH = 92f;
                float width = MeasureText(s, glyphH, 0.18f) - glyphH / 1.6f * 0.18f;
                float x = (w - width) * 0.5f, y = (h - glyphH) * 0.5f;
                DrawText(px, w, h, s, x, y, glyphH, 22f, outline, 0.18f);
                DrawText(px, w, h, s, x, y, glyphH, 13f, fill, 0.18f);
                t.SetPixels32(px);
                t.Apply(true, true);
                return t;
            });

        /// <summary>LED advertising board strip (original fictional sponsors).</summary>
        public static Texture2D LedBoard(string[] messages, Color bg, Color fg, Color accent) =>
            Cached("led" + string.Join("|", messages) + ColorUtility.ToHtmlStringRGB(bg), () =>
            {
                const int w = 1024, h = 64;
                var t = New(w, h, false, true);
                var px = new Color32[w * h];
                for (int i = 0; i < px.Length; i++)
                {
                    int x = i % w, y = i / w;
                    float g = 0.85f + 0.15f * (y / (float)h);
                    px[i] = (Color32)(bg * g);
                }
                float seg = w / (float)messages.Length;
                for (int m = 0; m < messages.Length; m++)
                {
                    float tw = MeasureText(messages[m], 36f) ;
                    float x = m * seg + (seg - tw) * 0.5f;
                    DrawText(px, w, h, messages[m], x, 14f, 36f, 6.5f, m % 2 == 0 ? fg : accent, 0.28f, 0.18f);
                }
                t.SetPixels32(px);
                t.Apply(true, true);
                return t;
            });

        /// <summary>Soft round blob (contact shadows, selection rings).</summary>
        public static Texture2D Ring(bool filled) => Cached(filled ? "blob" : "ring", () =>
        {
            const int s = 128;
            var t = New(s, s, true, true, TextureWrapMode.Clamp);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2 - 1, dy = (y + 0.5f) / s * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = filled ? Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d) : Mathf.Clamp01(1 - Mathf.Abs(d - 0.82f) / 0.1f);
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        });

        /// <summary>Rounded rectangle for UI (9-slice friendly), white with alpha.</summary>
        public static Texture2D RoundedRect(int size, float radius, float border = 0f) => Cached($"rr{size}_{radius}_{border}", () =>
        {
            var t = New(size, size, true, false, TextureWrapMode.Clamp);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0);
                    float qy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0);
                    float d = Mathf.Sqrt(qx * qx + qy * qy) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (border > 0) a *= Mathf.Clamp01(d + border + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        });

        /// <summary>Circle for touch buttons (filled disc with soft edge).</summary>
        public static Texture2D Disc(int size = 128, float ring = 0f) => Cached($"disc{size}_{ring}", () =>
        {
            var t = New(size, size, true, true, TextureWrapMode.Clamp);
            var px = new Color32[size * size];
            float r = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - size * 0.5f) * (x + 0.5f - size * 0.5f) + (y + 0.5f - size * 0.5f) * (y + 0.5f - size * 0.5f));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (ring > 0) a *= Mathf.Clamp01(d - (r - ring) + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        });

        /// <summary>Vertical gradient (UI backgrounds, sky).</summary>
        public static Texture2D Gradient(Color top, Color bottom, int h = 128) => Cached($"grad{ColorUtility.ToHtmlStringRGBA(top)}{ColorUtility.ToHtmlStringRGBA(bottom)}{h}", () =>
        {
            var t = New(2, h, true, false, TextureWrapMode.Clamp);
            var px = new Color32[2 * h];
            for (int y = 0; y < h; y++)
            {
                var c = (Color32)Color.Lerp(bottom, top, y / (float)(h - 1));
                px[y * 2] = c; px[y * 2 + 1] = c;
            }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        });

        public static Texture2D Solid(Color c) => Cached("solid" + ColorUtility.ToHtmlStringRGBA(c), () =>
        {
            var t = New(2, 2, true, false, TextureWrapMode.Clamp);
            t.SetPixels32(new[] { (Color32)c, (Color32)c, (Color32)c, (Color32)c });
            t.Apply(false, true);
            return t;
        });

        /// <summary>Skin texture with subtle pore/tone variation (avoids plastic look).</summary>
        public static Texture2D SkinDetail() => Cached("skin", () =>
        {
            const int s = 128;
            var t = New(s, s, false);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float n = Fbm(x / (float)s, y / (float)s, 21, 16, 3);
                    byte v = (byte)(232 + n * 23);
                    px[y * s + x] = new Color32(v, v, v, 255);
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        });

        /// <summary>Fabric weave for kits.</summary>
        public static Texture2D Fabric() => Cached("fabric", () =>
        {
            const int s = 64;
            var t = New(s, s, false);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float weave = ((x / 2 + y / 2) % 2 == 0) ? 1f : 0.93f;
                    float n = Fbm(x / (float)s, y / (float)s, 5, 8, 2);
                    byte v = (byte)(Mathf.Clamp01(weave * (0.9f + n * 0.1f)) * 255);
                    px[y * s + x] = new Color32(v, v, v, 255);
                }
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        });
    }
}
