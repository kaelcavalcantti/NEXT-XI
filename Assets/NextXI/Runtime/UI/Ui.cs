using System;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>
    /// NEXT XI visual language for the immediate-mode UI: palette, typography (Barlow, SIL OFL),
    /// rounded surfaces, touch-sized controls and resolution scaling. All screens draw through
    /// these helpers so the look stays consistent.
    /// </summary>
    public static class Ui
    {
        // Palette: deep navy night, signal orange, electric cyan.
        public static readonly Color Bg = new Color32(8, 12, 26, 255);
        public static readonly Color Panel = new Color32(18, 25, 44, 236);
        public static readonly Color PanelHi = new Color32(28, 38, 64, 245);
        public static readonly Color Line = new Color32(46, 60, 94, 255);
        public static readonly Color Accent = new Color32(255, 106, 19, 255);
        public static readonly Color AccentDark = new Color32(196, 70, 0, 255);
        public static readonly Color Cyan = new Color32(34, 211, 238, 255);
        public static readonly Color Text = new Color32(245, 247, 250, 255);
        public static readonly Color Muted = new Color32(140, 150, 170, 255);
        public static readonly Color Good = new Color32(34, 197, 94, 255);
        public static readonly Color Bad = new Color32(239, 68, 68, 255);
        public static readonly Color Gold = new Color32(250, 204, 21, 255);

        private static Font _display, _displayItalic, _body;
        private static GUIStyle _label;
        private static float _lastScale = -1f;
        private static Texture2D _rr, _rrBorder, _white, _disc;

        /// <summary>UI scale: 1 at 720 px of height (landscape phone baseline).</summary>
        public static float S { get; private set; } = 1f;
        public static float W => Screen.width / S;
        public static float H => Screen.height / S;

        public static void Begin()
        {
            float s = Mathf.Max(0.75f, Mathf.Min(Screen.height / 720f, Screen.width / 1280f));
            if (Math.Abs(s - _lastScale) > 0.001f || _label == null) Init(s);
            // Rects are authored in virtual units and scaled here (no GUI.matrix scaling, which blurs text).
            GUI.matrix = Matrix4x4.identity;
        }

        private static void Init(float s)
        {
            S = s;
            _lastScale = s;
            _display = Resources.Load<Font>("Fonts/BarlowCondensed-Bold");
            _displayItalic = Resources.Load<Font>("Fonts/BarlowCondensed-ExtraBoldItalic");
            _body = Resources.Load<Font>("Fonts/Barlow-Medium");
            _rr = ProcTex.RoundedRect(64, 16f);
            _rrBorder = ProcTex.RoundedRect(64, 16f, 3f);
            _white = ProcTex.Solid(Color.white);
            _disc = ProcTex.Disc(128);
            _label = new GUIStyle { richText = true, wordWrap = false, clipping = TextClipping.Overflow };
            _label.normal.textColor = Text;
        }

        public static Texture2D White => _white;

        /// <summary>Virtual rect (720p-based units) to screen pixels.</summary>
        public static Rect P(Rect r) => new Rect(r.x * S, r.y * S, r.width * S, r.height * S);
        public static Vector2 Mouse => Event.current.mousePosition / S;
        public static Texture2D DiscTex => _disc;

        public enum FontKind { Display, DisplayItalic, Body }

        public static GUIStyle Style(int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, FontKind kind = FontKind.Display, bool wrap = false)
        {
            var st = new GUIStyle(_label)
            {
                fontSize = Mathf.Max(8, Mathf.RoundToInt(size * S)),
                alignment = anchor,
                wordWrap = wrap,
                font = kind == FontKind.Display ? _display : kind == FontKind.DisplayItalic ? _displayItalic : _body
            };
            st.normal.textColor = color;
            return st;
        }

        public static void Label(Rect r, string text, int size = 22, Color? color = null, TextAnchor anchor = TextAnchor.MiddleLeft, FontKind kind = FontKind.Display, bool wrap = false)
            => GUI.Label(P(r), text, Style(size, color ?? Text, anchor, kind, wrap));

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(P(r), _white);
            GUI.color = old;
        }

        public static void Round(Rect r, Color c, bool border = false)
        {
            var old = GUI.color; GUI.color = c;
            // IMGUI draws rounded rectangles/outlines natively (radius and border width in pixels).
            GUI.DrawTexture(P(r), _white, ScaleMode.StretchToFill, true, 0, c, border ? 2f * S : 0f, Mathf.Min(12f, r.height * 0.5f) * S);
            GUI.color = old;
        }

        public static void PanelBox(Rect r, bool hi = false)
        {
            Round(r, hi ? PanelHi : Panel);
            Round(r, Line, true);
        }

        public static void Disc(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(P(r), _disc);
            GUI.color = old;
        }

        /// <summary>Large touch button. Returns true when clicked/tapped.</summary>
        public static bool Button(Rect r, string text, bool primary = false, int size = 24, bool enabled = true)
        {
            bool hover = r.Contains(Mouse);
            Color baseC = primary ? Accent : PanelHi;
            if (!enabled) baseC = new Color(0.2f, 0.22f, 0.28f, 0.8f);
            else if (hover) baseC = primary ? Color.Lerp(Accent, Color.white, 0.12f) : Color.Lerp(PanelHi, Cyan, 0.12f);
            Round(r, baseC);
            if (!primary) Round(r, hover && enabled ? Cyan : Line, true);
            Label(r, text, size, enabled ? Text : Muted, TextAnchor.MiddleCenter);
            if (!enabled) return false;
            bool clicked = GUI.Button(P(r), GUIContent.none, GUIStyle.none);
            if (clicked) AudioService.Instance?.PlayUi(primary ? UiSound.Confirm : UiSound.Click);
            return clicked;
        }

        public static bool Chip(Rect r, string text, bool selected, int size = 20)
        {
            Round(r, selected ? Accent : Panel);
            Round(r, selected ? Accent : Line, true);
            Label(r, text, size, selected ? Text : Muted, TextAnchor.MiddleCenter);
            bool c = GUI.Button(P(r), GUIContent.none, GUIStyle.none);
            if (c) AudioService.Instance?.PlayUi(UiSound.Click);
            return c;
        }

        /// <summary>Segmented selector; returns the new index.</summary>
        public static int Segmented(Rect r, int index, string[] options, int size = 20)
        {
            float w = r.width / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                var cr = new Rect(r.x + i * w + 2, r.y, w - 4, r.height);
                if (Chip(cr, options[i], i == index, size)) index = i;
            }
            return index;
        }

        /// <summary>Touch-friendly slider (thick track, big thumb).</summary>
        public static float Slider(Rect r, float value, string label, string valueText = null)
        {
            Label(new Rect(r.x, r.y, r.width * 0.45f, r.height), label, 20, Muted);
            var track = new Rect(r.x + r.width * 0.45f, r.y + r.height * 0.5f - 4, r.width * 0.42f, 8);
            Round(track, Line);
            Round(new Rect(track.x, track.y, track.width * Mathf.Clamp01(value), track.height), Accent);
            var thumb = new Rect(track.x + track.width * Mathf.Clamp01(value) - 14, r.y + r.height * 0.5f - 14, 28, 28);
            Disc(thumb, Text);
            Label(new Rect(track.xMax + 10, r.y, r.width * 0.13f - 10, r.height), valueText ?? Mathf.RoundToInt(value * 100) + "%", 20, Text, TextAnchor.MiddleRight);
            var hit = new Rect(track.x - 14, r.y, track.width + 28, r.height);
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && hit.Contains(Mouse))
            {
                value = Mathf.Clamp01((Mouse.x - track.x) / track.width);
                e.Use();
                GUI.changed = true;
            }
            return value;
        }

        public static bool Toggle(Rect r, bool value, string label)
        {
            Label(new Rect(r.x, r.y, r.width - 90, r.height), label, 20, Muted);
            var sw = new Rect(r.xMax - 76, r.y + r.height * 0.5f - 16, 70, 32);
            Round(sw, value ? Accent : Line);
            Disc(new Rect(value ? sw.xMax - 30 : sw.x + 2, sw.y + 2, 28, 28), Text);
            if (GUI.Button(P(r), GUIContent.none, GUIStyle.none))
            {
                AudioService.Instance?.PlayUi(UiSound.Click);
                return !value;
            }
            return value;
        }

        /// <summary>The NEXT XI wordmark: italic display type with an orange chevron emblem.</summary>
        public static void Logo(Rect r, float alpha = 1f)
        {
            float h = r.height;
            // Emblem: two stacked chevrons in a shield-like disc.
            var emblem = new Rect(r.x, r.y, h, h);
            Disc(emblem, new Color(Accent.r, Accent.g, Accent.b, alpha));
            Disc(new Rect(emblem.x + h * 0.08f, emblem.y + h * 0.08f, h * 0.84f, h * 0.84f), new Color(Bg.r, Bg.g, Bg.b, alpha));
            Label(new Rect(emblem.x, emblem.y - h * 0.04f, h, h), "XI", Mathf.RoundToInt(h * 0.5f), new Color(1, 1, 1, alpha), TextAnchor.MiddleCenter, FontKind.DisplayItalic);
            Label(new Rect(emblem.xMax + h * 0.2f, r.y - h * 0.08f, r.width, h), "NEXT", Mathf.RoundToInt(h * 0.82f), new Color(1, 1, 1, alpha), TextAnchor.MiddleLeft, FontKind.DisplayItalic);
            float nextW = h * 1.85f;
            Label(new Rect(emblem.xMax + h * 0.2f + nextW, r.y - h * 0.08f, r.width, h), "XI", Mathf.RoundToInt(h * 0.82f), new Color(Accent.r, Accent.g, Accent.b, alpha), TextAnchor.MiddleLeft, FontKind.DisplayItalic);
        }

        public static void Gradient(Rect r, Color top, Color bottom)
        {
            GUI.DrawTexture(P(r), ProcTex.Gradient(top, bottom));
        }
    }
}
