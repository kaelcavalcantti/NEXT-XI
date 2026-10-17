using System.Collections.Generic;
using NextXI.Core.Match;
using NextXI.Core.Settings;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Raw control intent for one frame (screen-space stick, button edges).</summary>
    public struct InputFrame
    {
        public Vector2 Stick;
        public bool Sprint;
        public bool Pass, Through, Lob, Shoot, Skill, Switch;
        public float Power;
        public bool ShieldHeld;
        public bool Pause, CameraCycle;
        public bool AnyTouch;
    }

    /// <summary>
    /// On-screen touch controls: floating virtual stick (left), contextual action buttons
    /// (right) with hold-to-power, sprint and shield holds, pause and camera buttons.
    /// Labels change with the situation (attack, support, defence, set piece). Size,
    /// position offsets, handedness and dead zone come from ControlSettings. Keyboard and
    /// mouse work too (editor/desktop testing).
    /// </summary>
    public sealed class TouchControls
    {
        public enum Btn { Shoot, Pass, Through, Lob, Sprint, Skill, Shield, Count }

        private sealed class ButtonState
        {
            public Rect Rect;             // virtual units
            public int FingerId = -1;
            public float DownTime;
            public bool Held;
            public bool ReleasedThisFrame;
            public float HeldFor;
        }

        public ControlSettings Settings;
        private readonly ButtonState[] _b = new ButtonState[(int)Btn.Count];
        private int _stickFinger = -1;
        private Vector2 _stickOrigin, _stickPos;
        private bool _stickActive;
        private bool _mouseStick;
        private float _keyShootDown = -1f, _keyLobDown = -1f, _keyThroughDown = -1f, _keyPassDown = -1f;
        private Rect _pauseRect, _camRect;
        public UserContext Context = UserContext.Attacking;
        public bool Visible = true;
        /// <summary>Shot power gauge 0..1 while a power button is held (for the HUD).</summary>
        public float ChargingPower { get; private set; }

        public TouchControls(ControlSettings settings)
        {
            Settings = settings;
            for (int i = 0; i < _b.Length; i++) _b[i] = new ButtonState();
            Input.multiTouchEnabled = true;
        }

        private float BtnSize => 96f * Settings.ButtonScale;

        /// <summary>Lays out buttons in virtual (720p) coordinates.</summary>
        public void Layout()
        {
            float W = Ui.W, H = Ui.H;
            float s = BtnSize;
            bool left = Settings.LeftHanded;
            Vector2 anchor = new Vector2(left ? 40f + s * 1.3f : W - 40f - s * 1.3f, H - 40f - s * 0.65f);
            Rect At(float dx, float dy, float scale)
            {
                float x = anchor.x + (left ? -dx : dx) * s;
                float y = anchor.y + dy * s;
                float sz = s * scale;
                return new Rect(x - sz * 0.5f, y - sz * 0.5f, sz, sz);
            }
            _b[(int)Btn.Shoot].Rect = At(0.55f, -0.05f, 1.15f);
            _b[(int)Btn.Pass].Rect = At(-0.65f, 0.15f, 1.0f);
            _b[(int)Btn.Through].Rect = At(-0.45f, -1.05f, 0.95f);
            _b[(int)Btn.Lob].Rect = At(0.6f, -1.25f, 0.95f);
            _b[(int)Btn.Sprint].Rect = At(-1.75f, 0.25f, 0.95f);
            _b[(int)Btn.Skill].Rect = At(-1.55f, -0.85f, 0.8f);
            _b[(int)Btn.Shield].Rect = At(-0.05f, -2.05f, 0.75f);
            for (int i = 0; i < _b.Length; i++)
            {
                var r = _b[i].Rect;
                r.x += Settings.ButtonOffsetsX[i] * W;
                r.y += Settings.ButtonOffsetsY[i] * H;
                _b[i].Rect = r;
            }
            _pauseRect = new Rect(W - 86f, 14f, 70f, 56f);
            _camRect = new Rect(W - 166f, 14f, 70f, 56f);
        }

        /// <summary>Reads touches/mouse/keyboard. Call once per frame from Update.</summary>
        public InputFrame Poll()
        {
            Layout();
            var f = new InputFrame();
            foreach (var b in _b) b.ReleasedThisFrame = false;
            float now = Time.unscaledTime;
            float S = Ui.S;

            // ---- touches
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                var p = new Vector2(t.position.x, Screen.height - t.position.y) / S;
                f.AnyTouch = true;
                if (t.phase == TouchPhase.Began) OnDown(t.fingerId, p, now, ref f);
                else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) OnMove(t.fingerId, p);
                else OnUp(t.fingerId, now);
            }
            // ---- mouse (editor/desktop): left button acts as one finger
            if (Input.touchCount == 0)
            {
                var mp = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / S;
                if (Input.GetMouseButtonDown(0)) OnDown(99, mp, now, ref f);
                else if (Input.GetMouseButton(0)) OnMove(99, mp);
                else if (Input.GetMouseButtonUp(0)) OnUp(99, now);
            }

            // ---- stick
            if (_stickActive)
            {
                float radius = 70f * Settings.StickScale;
                Vector2 d = (_stickPos - _stickOrigin) / radius;
                if (d.magnitude > 1f) d.Normalize();
                float dead = Mathf.Lerp(0.18f, 0.05f, Settings.Sensitivity);
                float m = d.magnitude;
                f.Stick = m < dead ? Vector2.zero : d.normalized * Mathf.InverseLerp(dead, 1f, m);
                f.Stick.y = -f.Stick.y; // screen y grows downwards
            }

            // ---- keyboard
            Vector2 k = Vector2.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) k.y += 1;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) k.y -= 1;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) k.x += 1;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) k.x -= 1;
            if (k.sqrMagnitude > 0) f.Stick = k.normalized;
            KeyPower(KeyCode.K, ref _keyShootDown, now, ref f.Shoot, ref f.Power);
            KeyPower(KeyCode.I, ref _keyLobDown, now, ref f.Lob, ref f.Power);
            KeyPower(KeyCode.L, ref _keyThroughDown, now, ref f.Through, ref f.Power);
            KeyPower(KeyCode.J, ref _keyPassDown, now, ref f.Pass, ref f.Power);
            if (Input.GetKeyDown(KeyCode.U)) f.Skill = true;
            if (Input.GetKeyDown(KeyCode.Q)) f.Switch = true;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) f.Pause = true;
            if (Input.GetKeyDown(KeyCode.C)) f.CameraCycle = true;

            // ---- buttons
            f.Sprint |= _b[(int)Btn.Sprint].Held || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            f.ShieldHeld |= _b[(int)Btn.Shield].Held || Input.GetKey(KeyCode.E);
            ChargingPower = 0f;
            foreach (var bi in new[] { Btn.Shoot, Btn.Lob, Btn.Through, Btn.Pass })
            {
                var b = _b[(int)bi];
                if (b.Held) ChargingPower = Mathf.Max(ChargingPower, PowerFor(bi, now - b.DownTime));
                if (!b.ReleasedThisFrame) continue;
                float power = PowerFor(bi, b.HeldFor);
                switch (bi)
                {
                    case Btn.Shoot: f.Shoot = true; break;
                    case Btn.Lob: if (Context == UserContext.Defending) f.Switch = true; else f.Lob = true; break;
                    case Btn.Through: f.Through = true; break;
                    case Btn.Pass: f.Pass = true; break;
                }
                f.Power = power;
            }
            if (_b[(int)Btn.Skill].ReleasedThisFrame) f.Skill = true;
            if (_keyShootDown >= 0) ChargingPower = Mathf.Max(ChargingPower, PowerFor(Btn.Shoot, now - _keyShootDown));
            return f;
        }

        private static float PowerFor(Btn b, float held)
        {
            // Passes reach full weight faster than shots.
            float full = b == Btn.Pass ? 0.55f : 0.85f;
            return Mathf.Clamp01(0.15f + held / full * 0.85f);
        }

        private void KeyPower(KeyCode key, ref float downAt, float now, ref bool fire, ref float power)
        {
            if (Input.GetKeyDown(key)) downAt = now;
            if (Input.GetKeyUp(key) && downAt >= 0)
            {
                fire = true;
                power = PowerFor(key == KeyCode.J ? Btn.Pass : Btn.Shoot, now - downAt);
                downAt = -1f;
            }
        }

        private void OnDown(int finger, Vector2 p, float now, ref InputFrame f)
        {
            if (_pauseRect.Contains(p)) { f.Pause = true; return; }
            if (_camRect.Contains(p)) { f.CameraCycle = true; return; }
            for (int i = 0; i < _b.Length; i++)
            {
                if (!IsVisible((Btn)i)) continue;
                var r = _b[i].Rect;
                // Generous hit area (+18%) for thumbs.
                var hit = new Rect(r.x - r.width * 0.09f, r.y - r.height * 0.09f, r.width * 1.18f, r.height * 1.18f);
                if (hit.Contains(p) && _b[i].FingerId < 0)
                {
                    _b[i].FingerId = finger;
                    _b[i].Held = true;
                    _b[i].DownTime = now;
                    return;
                }
            }
            bool leftZone = Settings.LeftHanded ? p.x > Ui.W * 0.55f : p.x < Ui.W * 0.45f;
            if (leftZone && p.y > Ui.H * 0.2f && _stickFinger < 0)
            {
                _stickFinger = finger;
                _stickActive = true;
                _stickOrigin = Settings.FloatingStick ? p : DefaultStickCenter();
                _stickPos = p;
            }
        }

        private Vector2 DefaultStickCenter() => new Vector2(Settings.LeftHanded ? Ui.W - 190f : 190f, Ui.H - 190f);

        private void OnMove(int finger, Vector2 p)
        {
            if (finger == _stickFinger)
            {
                _stickPos = p;
                // Floating stick follows the thumb if dragged far beyond the radius.
                float radius = 70f * Settings.StickScale;
                Vector2 d = _stickPos - _stickOrigin;
                if (Settings.FloatingStick && d.magnitude > radius * 1.6f) _stickOrigin = _stickPos - d.normalized * radius * 1.6f;
            }
        }

        private void OnUp(int finger, float now)
        {
            if (finger == _stickFinger) { _stickFinger = -1; _stickActive = false; }
            foreach (var b in _b)
                if (b.FingerId == finger)
                {
                    b.FingerId = -1;
                    b.Held = false;
                    b.ReleasedThisFrame = true;
                    b.HeldFor = now - b.DownTime;
                }
        }

        private bool IsVisible(Btn b)
        {
            switch (Context)
            {
                case UserContext.None: return false;
                case UserContext.Defending: return b != Btn.Skill && b != Btn.Shield;
                case UserContext.SetPiece: return b == Btn.Shoot || b == Btn.Pass || b == Btn.Lob || b == Btn.Through;
                default: return true;
            }
        }

        private string LabelFor(Btn b)
        {
            switch (Context)
            {
                case UserContext.Defending:
                    switch (b)
                    {
                        case Btn.Shoot: return "CARRINHO";
                        case Btn.Pass: return "DESARME";
                        case Btn.Through: return "PRESSÃO";
                        case Btn.Lob: return "TROCAR";
                        case Btn.Sprint: return "CORRER";
                    }
                    break;
                case UserContext.SupportingAttack:
                    switch (b)
                    {
                        case Btn.Shoot: return "CHUTE";
                        case Btn.Pass: return "PEDIR";
                        case Btn.Through: return "INFILTRAR";
                        case Btn.Lob: return "LANÇAR";
                    }
                    break;
            }
            switch (b)
            {
                case Btn.Shoot: return "CHUTE";
                case Btn.Pass: return "PASSE";
                case Btn.Through: return "ENFIADA";
                case Btn.Lob: return Context == UserContext.SetPiece ? "CRUZAR" : "LANÇAR";
                case Btn.Sprint: return "CORRER";
                case Btn.Skill: return "DRIBLE";
                case Btn.Shield: return "PROTEGER";
            }
            return "";
        }

        /// <summary>Draws the controls (call from OnGUI after Ui.Begin()).</summary>
        public void Draw()
        {
            if (!Visible) return;
            Layout();
            bool contrast = Settings.HighContrastButtons;
            // Pause & camera
            Ui.Round(_pauseRect, new Color(0, 0, 0, 0.45f));
            Ui.Label(_pauseRect, "II", 30, Ui.Text, TextAnchor.MiddleCenter);
            Ui.Round(_camRect, new Color(0, 0, 0, 0.45f));
            Ui.Label(_camRect, "CAM", 20, Ui.Text, TextAnchor.MiddleCenter);
            if (Context == UserContext.None) return;

            // Stick
            float radius = 70f * Settings.StickScale;
            Vector2 c = _stickActive ? _stickOrigin : DefaultStickCenter();
            Ui.Disc(new Rect(c.x - radius, c.y - radius, radius * 2, radius * 2), new Color(1, 1, 1, _stickActive ? 0.16f : 0.08f));
            Vector2 knob = _stickActive ? _stickOrigin + Vector2.ClampMagnitude(_stickPos - _stickOrigin, radius) : c;
            Ui.Disc(new Rect(knob.x - radius * 0.45f, knob.y - radius * 0.45f, radius * 0.9f, radius * 0.9f), new Color(1, 1, 1, _stickActive ? 0.55f : 0.28f));

            for (int i = 0; i < _b.Length; i++)
            {
                var bt = (Btn)i;
                if (!IsVisible(bt)) continue;
                var r = _b[i].Rect;
                bool held = _b[i].Held;
                Color col = bt == Btn.Shoot ? Ui.Accent : (bt == Btn.Sprint || bt == Btn.Shield ? new Color(0.3f, 0.36f, 0.5f) : new Color(0.12f, 0.18f, 0.32f));
                col.a = contrast ? 0.95f : 0.72f;
                if (held) col = Color.Lerp(col, Color.white, 0.25f);
                float grow = held ? 0.94f : 1f;
                var rr = new Rect(r.center.x - r.width * grow * 0.5f, r.center.y - r.height * grow * 0.5f, r.width * grow, r.height * grow);
                Ui.Disc(rr, col);
                if (Settings.ShowButtonLabels) Ui.Label(rr, LabelFor(bt), Mathf.RoundToInt(18 * Settings.ButtonScale), Ui.Text, TextAnchor.MiddleCenter);
                // Power ring for chargeable buttons.
                if (held && (bt == Btn.Shoot || bt == Btn.Lob || bt == Btn.Through || bt == Btn.Pass))
                {
                    float pw = PowerFor(bt, Time.unscaledTime - _b[i].DownTime);
                    var bar = new Rect(rr.x, rr.yMax + 6, rr.width, 8);
                    Ui.Round(bar, new Color(0, 0, 0, 0.5f));
                    Ui.Round(new Rect(bar.x, bar.y, bar.width * pw, bar.height), pw > 0.85f ? Ui.Bad : Ui.Gold);
                }
            }
        }
    }

    /// <summary>Haptic feedback wrapper (respects the vibration setting at call sites).</summary>
    public static class Haptics
    {
        private static float _last;
        public static void Pulse()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Time.unscaledTime - _last < 0.15f) return;
            _last = Time.unscaledTime;
            Handheld.Vibrate();
#endif
        }
    }
}
