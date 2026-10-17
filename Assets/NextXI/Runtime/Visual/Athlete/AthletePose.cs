using NextXI.Core.Match;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Per-frame animation input derived from the simulation state of one player.</summary>
    public struct AnimInput
    {
        /// <summary>Velocity in character space (x = right, z = forward), m/s.</summary>
        public Vector3 LocalVelocity;
        /// <summary>Forward acceleration (m/s^2), used for lean.</summary>
        public float Accel;
        /// <summary>Yaw rate (rad/s, positive = turning right).</summary>
        public float TurnRate;
        public PlayerAction Action;
        public float ActionTime, ActionDuration, ContactTime;
        /// <summary>+1 kicks with the right foot, -1 with the left.</summary>
        public int KickSide;
        /// <summary>+1 dive/slide to the character's right, -1 to the left.</summary>
        public int DiveSide;
        public float JumpHeight;
        public bool HasBall, Jockey, Shield, Goalkeeper, Stunned;
        public float Fatigue;
        public float Time;
        /// <summary>Head look yaw towards the ball (degrees, + = right).</summary>
        public float LookYaw;
        /// <summary>Kick type power (0..1) changes amplitude: passes are compact, shots full.</summary>
        public float KickPower;
    }

    /// <summary>
    /// Procedural animation solver. Produces local Euler rotations for every bone of the
    /// athlete skeleton from velocity and action state: a locomotion layer (walk/jog/sprint,
    /// strafe, backpedal, turn lean, idle, jockey, shield) blended with an action layer
    /// (kicks timed to the simulation contact frame, headers, tackles, slides, dives, throws,
    /// falls, celebrations). Rotation conventions (bind pose is world aligned, facing +Z):
    /// limbs hanging down flex forward with -X; spine/neck bend forward with +X; +Y yaws right;
    /// +Z on an upright bone rolls the top to the character's left.
    /// </summary>
    public sealed class AthletePose
    {
        public const int Bones = (int)AthleteBone.Count;
        public readonly Vector3[] Euler = new Vector3[Bones];
        /// <summary>Offset of the hips from the bind position (metres, character space).</summary>
        public Vector3 HipsOffset;

        private readonly Vector3[] _loco = new Vector3[Bones];
        private readonly Vector3[] _act = new Vector3[Bones];
        private Vector3 _locoHips, _actHips;
        private float _phase;
        private float _actionWeight;
        private PlayerAction _lastAction;
        private bool _initialised;
        private readonly float _hipsHeight;

        public float Phase => _phase;

        public AthletePose(float hipsHeight) { _hipsHeight = hipsHeight; }

        private static void Clear(Vector3[] a) { for (int i = 0; i < a.Length; i++) a[i] = Vector3.zero; }

        public void Update(in AnimInput i, float dt)
        {
            Clear(_loco);
            Clear(_act);
            _locoHips = Vector3.zero;
            _actHips = Vector3.zero;
            Locomotion(i, dt);

            bool hasAction = i.Action != PlayerAction.None;
            float targetW = hasAction ? 1f : 0f;
            if (hasAction) Action(i);
            if (i.Action != _lastAction && hasAction) _actionWeight = Mathf.Min(_actionWeight, 0.35f);
            // Fast blend in (actions must hit their contact frame), slower blend out.
            float rate = targetW > _actionWeight ? 14f : 7f;
            _actionWeight = Mathf.MoveTowards(_actionWeight, targetW, rate * dt);
            if (hasAction) _lastAction = i.Action;
            float w = MathfEase(_actionWeight);

            float smooth = 1f - Mathf.Exp(-(hasAction ? 26f : 16f) * dt);
            if (!_initialised) { smooth = 1f; _initialised = true; }
            for (int b = 0; b < Bones; b++)
            {
                Vector3 target = Vector3.Lerp(_loco[b], _act[b], hasAction || _actionWeight > 0 ? w : 0f);
                Euler[b] = Vector3.Lerp(Euler[b], target, smooth);
            }
            Vector3 ht = Vector3.Lerp(_locoHips, _actHips, w);
            HipsOffset = Vector3.Lerp(HipsOffset, ht, smooth);
        }

        private static float MathfEase(float t) => t * t * (3f - 2f * t);

        private static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        // ================================================================== locomotion

        private void Locomotion(in AnimInput i, float dt)
        {
            Vector3 v = i.LocalVelocity; v.y = 0;
            float s = v.magnitude;
            float r = Smooth(1.6f, 3.6f, s);        // walk -> run
            float q = Smooth(6.2f, 8.8f, s);        // run -> sprint
            float stride = 1.25f + 0.33f * s;      // metres per full gait cycle
            _phase = (_phase + s * dt / stride) % 1f;
            float fwd = s > 0.05f ? v.z / s : 1f;
            float side = s > 0.05f ? v.x / s : 0f;
            float moving = Smooth(0.15f, 0.9f, s);

            float twoPi = Mathf.PI * 2f;
            float ampF = Mathf.Lerp(Mathf.Lerp(24f, 42f, r), 62f, q);
            float ampB = ampF * Mathf.Lerp(0.6f, 0.5f, r);
            float kneeAmp = Mathf.Lerp(Mathf.Lerp(50f, 92f, r), 125f, q);
            float fatigue = Mathf.Clamp01(i.Fatigue);

            for (int leg = 0; leg < 2; leg++)
            {
                float ph = _phase + (leg == 0 ? 0f : 0.5f);
                float sw = Mathf.Sin(ph * twoPi);         // +1 leg forward
                float swingVel = Mathf.Cos(ph * twoPi);   // > 0 leg moving forward (swing)
                int thigh = leg == 0 ? (int)AthleteBone.LThigh : (int)AthleteBone.RThigh;
                int shin = thigh + 1, foot = thigh + 2;
                float legSign = leg == 0 ? -1f : 1f;      // -1 left side (-X)

                float pitch = (sw >= 0 ? sw * ampF : sw * ampB) * Mathf.Abs(fwd);
                if (fwd < -0.2f) pitch *= 0.7f; // backpedal: shorter steps
                float swingDirSign = fwd >= 0 ? 1f : -1f;
                float thighPitch = -pitch * swingDirSign;
                // Side-steps: alternate abduction instead of flexion.
                float thighRoll = side * sw * 22f * moving;
                float knee = 6f + kneeAmp * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(ph * twoPi + 1.35f)), 1.6f) * moving;
                knee += r * 14f * Mathf.Max(0f, sw) * moving; // stance absorption
                float ankle = (-12f * sw + (sw < -0.4f ? 30f * (-sw - 0.4f) * r : 0f)) * moving;

                _loco[thigh] = new Vector3(thighPitch * moving + 4f * r, 0, thighRoll + legSign * -1.5f);
                _loco[shin] = new Vector3(knee, 0, 0);
                _loco[foot] = new Vector3(ankle - knee * 0.25f * (1 - r), 0, 0);
                if (swingVel < 0 && s < 0.2f) _loco[foot] = Vector3.zero;
            }

            float pelvisYaw = Mathf.Sin(_phase * twoPi) * Mathf.Lerp(6f, 9f, r) * moving * fwd;
            float bob = moving * (r > 0.5f
                ? 0.035f * Mathf.Abs(Mathf.Sin(_phase * twoPi)) - 0.03f - 0.02f * q
                : -0.012f * (1 - Mathf.Cos(_phase * twoPi * 2f)) * 0.5f);
            _locoHips = new Vector3(0, bob, 0);
            float lean = 3f * moving + 6f * r + 7f * q + Mathf.Clamp(i.Accel * 1.6f, -8f, 10f) + fatigue * 5f;
            if (fwd < -0.2f) lean = -3f;
            float turnLean = Mathf.Clamp(i.TurnRate * s * 1.3f, -16f, 16f);
            _loco[(int)AthleteBone.Hips] = new Vector3(lean * 0.35f, pelvisYaw, -turnLean);
            _loco[(int)AthleteBone.Spine] = new Vector3(lean * 0.35f, -pelvisYaw * 0.6f, 0);
            _loco[(int)AthleteBone.Chest] = new Vector3(lean * 0.3f, -pelvisYaw * 0.9f, 0);
            _loco[(int)AthleteBone.Neck] = new Vector3(-lean * 0.45f, i.LookYaw * 0.35f, 0);
            _loco[(int)AthleteBone.Head] = new Vector3(-lean * 0.25f + (i.HasBall && s < 4f ? 12f : 0f), i.LookYaw * 0.45f, 0);

            float armAmp = Mathf.Lerp(Mathf.Lerp(16f, 38f, r), 58f, q) * (1f - fatigue * 0.3f) * moving;
            float elbow = Mathf.Lerp(Mathf.Lerp(12f, 80f, r), 95f, q);
            for (int arm = 0; arm < 2; arm++)
            {
                // Each arm swings with the opposite leg.
                float ph = _phase + (arm == 0 ? 0.5f : 0f);
                float sw = Mathf.Sin(ph * twoPi);
                int ua = arm == 0 ? (int)AthleteBone.LUpperArm : (int)AthleteBone.RUpperArm;
                int fa = ua + 1;
                float sideSign = arm == 0 ? -1f : 1f;
                float swingPitch = -sw * armAmp * Mathf.Abs(fwd) + 4f * r;
                _loco[ua] = new Vector3(swingPitch, 0, sideSign * (3f + 4f * r) * -1f + sideSign * 6f);
                _loco[fa] = new Vector3(-(elbow + sw * 10f * r), 0, 0);
                _loco[ua + 2] = new Vector3(0, 0, 0);
            }

            // Idle breathing and weight shift.
            float idle = 1f - moving;
            if (idle > 0f)
            {
                float br = Mathf.Sin(i.Time * 1.9f);
                _loco[(int)AthleteBone.Chest] += new Vector3(br * 1.2f, 0, 0) * idle;
                _loco[(int)AthleteBone.Hips] += new Vector3(0, 0, Mathf.Sin(i.Time * 0.6f) * 2f) * idle;
                _loco[(int)AthleteBone.LShin] += new Vector3(6f, 0, 0) * idle;
                _loco[(int)AthleteBone.RShin] += new Vector3(9f, 0, 0) * idle;
                _loco[(int)AthleteBone.LThigh] += new Vector3(-4f, 0, -2f) * idle;
                _loco[(int)AthleteBone.RThigh] += new Vector3(-5f, 0, 3f) * idle;
                _loco[(int)AthleteBone.LForeArm] += new Vector3(-10f, 0, 0) * idle;
                _loco[(int)AthleteBone.RForeArm] += new Vector3(-10f, 0, 0) * idle;
                _locoHips.y -= 0.012f * idle;
                if (i.Goalkeeper)
                {
                    // Set position: crouched, hands ready.
                    Ready(idle, 1f);
                }
            }

            if (i.Jockey) Ready(1f, 0.8f);
            if (i.Shield)
            {
                _loco[(int)AthleteBone.LThigh] += new Vector3(-8f, 0, -10f);
                _loco[(int)AthleteBone.RThigh] += new Vector3(-8f, 0, 10f);
                _loco[(int)AthleteBone.LShin] += new Vector3(18f, 0, 0);
                _loco[(int)AthleteBone.RShin] += new Vector3(18f, 0, 0);
                _loco[(int)AthleteBone.LUpperArm] += new Vector3(-10f, 0, -35f);
                _loco[(int)AthleteBone.RUpperArm] += new Vector3(-10f, 0, 35f);
                _loco[(int)AthleteBone.Spine] += new Vector3(-6f, 0, 0);
                _locoHips.y -= 0.05f;
            }
        }

        /// <summary>Athletic ready stance (jockeying, keeper set position).</summary>
        private void Ready(float k, float depth)
        {
            _loco[(int)AthleteBone.LThigh] += new Vector3(-28f * depth, 0, -9f) * k;
            _loco[(int)AthleteBone.RThigh] += new Vector3(-28f * depth, 0, 9f) * k;
            _loco[(int)AthleteBone.LShin] += new Vector3(48f * depth, 0, 0) * k;
            _loco[(int)AthleteBone.RShin] += new Vector3(48f * depth, 0, 0) * k;
            _loco[(int)AthleteBone.LFoot] += new Vector3(-18f * depth, 0, 0) * k;
            _loco[(int)AthleteBone.RFoot] += new Vector3(-18f * depth, 0, 0) * k;
            _loco[(int)AthleteBone.Hips] += new Vector3(22f * depth, 0, 0) * k;
            _loco[(int)AthleteBone.Neck] += new Vector3(-14f * depth, 0, 0) * k;
            _loco[(int)AthleteBone.Head] += new Vector3(-6f * depth, 0, 0) * k;
            _loco[(int)AthleteBone.LUpperArm] += new Vector3(-25f, 0, -22f) * k;
            _loco[(int)AthleteBone.RUpperArm] += new Vector3(-25f, 0, 22f) * k;
            _loco[(int)AthleteBone.LForeArm] += new Vector3(-35f, 0, 0) * k;
            _loco[(int)AthleteBone.RForeArm] += new Vector3(-35f, 0, 0) * k;
            _locoHips.y -= 0.11f * depth * k;
        }

        // ================================================================== actions

        private void Copy(Vector3[] from, Vector3[] to) { for (int b = 0; b < Bones; b++) to[b] = from[b]; }

        private void Action(in AnimInput i)
        {
            float d = Mathf.Max(0.05f, i.ActionDuration);
            float t = i.ActionTime;
            float p = Mathf.Clamp01(t / d);
            Copy(_loco, _act);
            _actHips = _locoHips;
            switch (i.Action)
            {
                case PlayerAction.Pass:
                case PlayerAction.Shot:
                case PlayerAction.LoftedPass:
                case PlayerAction.Cross:
                case PlayerAction.Clearance:
                case PlayerAction.KeeperKick:
                    Kick(i, t);
                    break;
                case PlayerAction.Volley:
                    Kick(i, t + i.ContactTime * 0.0f, volley: true);
                    break;
                case PlayerAction.Header:
                    Header(p);
                    break;
                case PlayerAction.Throw:
                case PlayerAction.KeeperThrow:
                    Throw(i, t);
                    break;
                case PlayerAction.Tackle:
                    Tackle(p, i.KickSide);
                    break;
                case PlayerAction.Slide:
                    Slide(p, i.KickSide);
                    break;
                case PlayerAction.KeeperDive:
                    Dive(p, i.DiveSide, i.JumpHeight);
                    break;
                case PlayerAction.KeeperCatch:
                    Catch(p);
                    break;
                case PlayerAction.Fall:
                    Fall(p);
                    break;
                case PlayerAction.Celebrate:
                    Celebrate(i.Time);
                    break;
                case PlayerAction.Dejected:
                    Dejected();
                    break;
                case PlayerAction.Trap:
                    _act[(int)AthleteBone.Spine] = new Vector3(-14f, 0, 0);
                    _act[(int)AthleteBone.Chest] = new Vector3(-10f, 0, 0);
                    _act[(int)AthleteBone.LUpperArm] = new Vector3(-10f, 0, -35f);
                    _act[(int)AthleteBone.RUpperArm] = new Vector3(-10f, 0, 35f);
                    break;
                case PlayerAction.Skill:
                    StepOver(p, i.KickSide);
                    break;
                case PlayerAction.Jump:
                    Header(Mathf.Min(p, 0.3f));
                    break;
            }
        }

        private void Kick(in AnimInput i, float t, bool volley = false)
        {
            int k = i.KickSide >= 0 ? 1 : -1;
            int kt = k > 0 ? (int)AthleteBone.RThigh : (int)AthleteBone.LThigh;
            int st = k > 0 ? (int)AthleteBone.LThigh : (int)AthleteBone.RThigh;
            int kArm = k > 0 ? (int)AthleteBone.RUpperArm : (int)AthleteBone.LUpperArm;
            int sArm = k > 0 ? (int)AthleteBone.LUpperArm : (int)AthleteBone.RUpperArm;
            float c = Mathf.Max(0.08f, i.ContactTime);
            bool pass = i.Action == PlayerAction.Pass;
            bool lofted = i.Action == PlayerAction.LoftedPass || i.Action == PlayerAction.Cross || i.Action == PlayerAction.Clearance || i.Action == PlayerAction.KeeperKick;
            float power = pass ? 0.55f : Mathf.Lerp(0.8f, 1f, i.KickPower);

            // Phase curves: back-lift until contact, strike through contact, follow-through.
            float back, strike;
            if (t < c) { back = MathfEase(t / c); strike = 0f; }
            else { back = Mathf.Clamp01(1f - (t - c) / 0.07f); strike = Mathf.Clamp01((t - c) / 0.22f); }
            float follow = MathfEase(strike);

            float thigh = 50f * back * power - (pass ? 35f : (lofted ? 80f : 65f)) * follow * power - (t >= c && strike < 0.3f ? 15f : 0f);
            float knee = 105f * back * power + 15f * (1 - back) - 10f * follow;
            if (volley) { thigh -= 35f; _actHips += new Vector3(0, 0.03f, 0); }
            _act[kt] = new Vector3(thigh, pass ? k * 30f : 0f, k * (volley ? 25f : 6f));
            _act[kt + 1] = new Vector3(Mathf.Max(5f, knee), 0, 0);
            _act[kt + 2] = new Vector3(pass ? -5f : 28f, pass ? k * 20f : 0f, 0);
            // Plant leg bends to absorb.
            _act[st] = new Vector3(-12f, 0, -k * 4f);
            _act[st + 1] = new Vector3(28f, 0, 0);
            _act[st + 2] = new Vector3(-12f, 0, 0);
            _actHips = new Vector3(0, -0.045f * power, 0);
            float bodyLean = lofted ? -10f * follow : 10f * follow + 4f * back;
            _act[(int)AthleteBone.Hips] = new Vector3(4f, -k * 12f * back + k * 18f * follow, k * (volley ? 22f : 5f));
            _act[(int)AthleteBone.Spine] = new Vector3(bodyLean * 0.5f + 4f, k * 8f * back - k * 10f * follow, 0);
            _act[(int)AthleteBone.Chest] = new Vector3(bodyLean * 0.5f, k * 10f * back - k * 12f * follow, 0);
            _act[(int)AthleteBone.Neck] = new Vector3(14f, 0, 0);
            _act[(int)AthleteBone.Head] = new Vector3(10f * (1 - follow), 0, 0);
            // Arms for balance: opposite arm out and forward, kicking-side arm back.
            float sSide = k > 0 ? -1f : 1f;
            _act[sArm] = new Vector3(-30f - 15f * follow, 0, sSide * (55f + 10f * power));
            _act[sArm + 1] = new Vector3(-25f, 0, 0);
            _act[kArm] = new Vector3(25f * back - 20f * follow, 0, k * 30f);
            _act[kArm + 1] = new Vector3(-30f, 0, 0);
        }

        private void Header(float p)
        {
            float load = Mathf.Clamp01(p / 0.2f);
            float snap = Mathf.Clamp01((p - 0.15f) / 0.2f);
            _act[(int)AthleteBone.Spine] = new Vector3(-18f * load + 34f * snap, 0, 0);
            _act[(int)AthleteBone.Chest] = new Vector3(-12f * load + 22f * snap, 0, 0);
            _act[(int)AthleteBone.Neck] = new Vector3(-15f * load + 25f * snap, 0, 0);
            _act[(int)AthleteBone.LUpperArm] = new Vector3(-40f, 0, -60f);
            _act[(int)AthleteBone.RUpperArm] = new Vector3(-40f, 0, 60f);
            _act[(int)AthleteBone.LForeArm] = new Vector3(-50f, 0, 0);
            _act[(int)AthleteBone.RForeArm] = new Vector3(-50f, 0, 0);
            _act[(int)AthleteBone.LThigh] = new Vector3(-20f, 0, -6f);
            _act[(int)AthleteBone.RThigh] = new Vector3(5f, 0, 6f);
            _act[(int)AthleteBone.LShin] = new Vector3(55f, 0, 0);
            _act[(int)AthleteBone.RShin] = new Vector3(80f, 0, 0);
        }

        private void Throw(in AnimInput i, float t)
        {
            float c = Mathf.Max(0.1f, i.ContactTime);
            float back = t < c ? MathfEase(t / c) : Mathf.Clamp01(1f - (t - c) / 0.1f);
            float fwd = t < c ? 0f : MathfEase(Mathf.Clamp01((t - c) / 0.25f));
            bool keeper = i.Action == PlayerAction.KeeperThrow;
            if (keeper)
            {
                // Bowling throw with the right arm.
                _act[(int)AthleteBone.RUpperArm] = new Vector3(70f * back - 150f * fwd, 0, 20f);
                _act[(int)AthleteBone.RForeArm] = new Vector3(-20f, 0, 0);
                _act[(int)AthleteBone.LUpperArm] = new Vector3(-60f * back, 0, -40f);
                _act[(int)AthleteBone.Chest] = new Vector3(10f * fwd, 20f * back - 15f * fwd, 0);
                _act[(int)AthleteBone.LThigh] = new Vector3(-30f * fwd, 0, -4f);
                _act[(int)AthleteBone.LShin] = new Vector3(30f * fwd, 0, 0);
                return;
            }
            // Two-handed throw-in from behind the head.
            float shoulder = -175f * back - 140f * fwd * (1 - back);
            float elbow = -110f * back - 20f * fwd;
            _act[(int)AthleteBone.LUpperArm] = new Vector3(shoulder, 0, -8f);
            _act[(int)AthleteBone.RUpperArm] = new Vector3(shoulder, 0, 8f);
            _act[(int)AthleteBone.LForeArm] = new Vector3(elbow, 0, 0);
            _act[(int)AthleteBone.RForeArm] = new Vector3(elbow, 0, 0);
            _act[(int)AthleteBone.Spine] = new Vector3(-16f * back + 14f * fwd, 0, 0);
            _act[(int)AthleteBone.Chest] = new Vector3(-12f * back + 12f * fwd, 0, 0);
            _act[(int)AthleteBone.LThigh] = new Vector3(-6f, 0, -3f);
            _act[(int)AthleteBone.RThigh] = new Vector3(14f, 0, 3f);
            _act[(int)AthleteBone.RShin] = new Vector3(22f, 0, 0);
        }

        private void Tackle(float p, int side)
        {
            int k = side >= 0 ? 1 : -1;
            int lead = k > 0 ? (int)AthleteBone.RThigh : (int)AthleteBone.LThigh;
            int sup = k > 0 ? (int)AthleteBone.LThigh : (int)AthleteBone.RThigh;
            float lunge = Mathf.Sin(Mathf.Clamp01(p / 0.8f) * Mathf.PI);
            _act[lead] = new Vector3(-58f * lunge, k * 18f, k * 12f * lunge);
            _act[lead + 1] = new Vector3(12f, 0, 0);
            _act[lead + 2] = new Vector3(-10f, k * 25f, 0);
            _act[sup] = new Vector3(-20f * lunge, 0, 0);
            _act[sup + 1] = new Vector3(55f * lunge, 0, 0);
            _actHips = new Vector3(0, -0.14f * lunge, 0);
            _act[(int)AthleteBone.Hips] = new Vector3(10f * lunge, k * 10f, 0);
            _act[(int)AthleteBone.Spine] = new Vector3(14f * lunge, 0, 0);
            _act[(int)AthleteBone.LUpperArm] = new Vector3(-20f, 0, -45f * lunge);
            _act[(int)AthleteBone.RUpperArm] = new Vector3(-20f, 0, 45f * lunge);
        }

        private void Slide(float p, int side)
        {
            int k = side >= 0 ? 1 : -1;
            int lead = k > 0 ? (int)AthleteBone.RThigh : (int)AthleteBone.LThigh;
            int trail = k > 0 ? (int)AthleteBone.LThigh : (int)AthleteBone.RThigh;
            int supArm = k > 0 ? (int)AthleteBone.LUpperArm : (int)AthleteBone.RUpperArm;
            int freeArm = k > 0 ? (int)AthleteBone.RUpperArm : (int)AthleteBone.LUpperArm;
            float down = Smooth(0f, 0.16f, p) * (1f - Smooth(0.78f, 1f, p));
            // Hips drop to ~0.25 m and the body lies back; the lead leg stays straight along the ground.
            _actHips = new Vector3(0, -(_hipsHeight - 0.26f) * down, 0);
            _act[(int)AthleteBone.Hips] = new Vector3(-62f * down, k * 10f * down, k * 8f * down);
            _act[(int)AthleteBone.Spine] = new Vector3(18f * down, 0, 0);
            _act[(int)AthleteBone.Chest] = new Vector3(12f * down, 0, 0);
            _act[(int)AthleteBone.Neck] = new Vector3(24f * down, 0, 0);
            _act[lead] = new Vector3(-28f * down, 0, k * 4f);
            _act[lead + 1] = new Vector3(4f, 0, 0);
            _act[lead + 2] = new Vector3(-15f * down, 0, 0);
            _act[trail] = new Vector3(20f * down, 0, -k * 12f * down);
            _act[trail + 1] = new Vector3(105f * down, 0, 0);
            _act[supArm] = new Vector3(45f * down, 0, -k * 30f * down);
            _act[supArm + 1] = new Vector3(-10f, 0, 0);
            _act[freeArm] = new Vector3(-70f * down, 0, k * 45f * down);
            _act[freeArm + 1] = new Vector3(-30f, 0, 0);
        }

        private void Dive(float p, int side, float jump)
        {
            int k = side >= 0 ? 1 : -1;
            float load = Smooth(0f, 0.12f, p);
            float fly = Smooth(0.08f, 0.35f, p);
            float land = Smooth(0.55f, 0.8f, p);
            // Body rolls towards the dive side until horizontal, then lies on the ground.
            float roll = 82f * fly;
            _act[(int)AthleteBone.Hips] = new Vector3(8f, 0, -k * roll);
            // Hips height: crouch, push off, then lie at ~0.18 m.
            float lying = -(_hipsHeight - 0.2f) * land;
            float flight = Mathf.Lerp(-0.12f * load, -(_hipsHeight - 0.55f) * fly, fly) ;
            _actHips = new Vector3(k * 0.35f * fly, Mathf.Min(flight, 0f) * (1 - land) + lying, 0);
            // Arms reach over the head towards the ball.
            float reach = fly;
            _act[(int)AthleteBone.LUpperArm] = new Vector3(-90f * reach, 0, k > 0 ? 70f * reach : -175f * reach);
            _act[(int)AthleteBone.RUpperArm] = new Vector3(-90f * reach, 0, k > 0 ? 175f * reach : -70f * reach);
            _act[(int)AthleteBone.LForeArm] = new Vector3(-10f, 0, 0);
            _act[(int)AthleteBone.RForeArm] = new Vector3(-10f, 0, 0);
            _act[(int)AthleteBone.Spine] = new Vector3(0, 0, -k * 10f * fly);
            _act[(int)AthleteBone.Neck] = new Vector3(0, 0, -k * 10f * fly);
            // Legs extended, slight bend of the lower leg.
            int top = k > 0 ? (int)AthleteBone.LThigh : (int)AthleteBone.RThigh;
            int bottom = k > 0 ? (int)AthleteBone.RThigh : (int)AthleteBone.LThigh;
            _act[top] = new Vector3(-10f * fly - 30f * load * (1 - fly), 0, k * 8f * fly);
            _act[top + 1] = new Vector3(20f * fly + 50f * load * (1 - fly), 0, 0);
            _act[bottom] = new Vector3(-25f * load * (1 - fly) + 10f * fly, 0, k * 4f);
            _act[bottom + 1] = new Vector3(15f + 45f * load * (1 - fly), 0, 0);
        }

        private void Catch(float p)
        {
            float c = Smooth(0f, 0.25f, p);
            _act[(int)AthleteBone.LUpperArm] = new Vector3(-55f * c, 0, -8f);
            _act[(int)AthleteBone.RUpperArm] = new Vector3(-55f * c, 0, 8f);
            _act[(int)AthleteBone.LForeArm] = new Vector3(-80f * c, -30f * c, 0);
            _act[(int)AthleteBone.RForeArm] = new Vector3(-80f * c, 30f * c, 0);
            _act[(int)AthleteBone.Spine] = new Vector3(14f * c, 0, 0);
            _act[(int)AthleteBone.LShin] = new Vector3(18f * c, 0, 0);
            _act[(int)AthleteBone.RShin] = new Vector3(18f * c, 0, 0);
            _act[(int)AthleteBone.LThigh] = new Vector3(-10f * c, 0, 0);
            _act[(int)AthleteBone.RThigh] = new Vector3(-10f * c, 0, 0);
            _actHips = new Vector3(0, -0.05f * c, 0);
        }

        private void Fall(float p)
        {
            float down = Smooth(0f, 0.3f, p) * (1f - Smooth(0.72f, 1f, p));
            _act[(int)AthleteBone.Hips] = new Vector3(78f * down, 0, 6f * down);
            _actHips = new Vector3(0, -(_hipsHeight - 0.2f) * down, -0.3f * down);
            _act[(int)AthleteBone.Spine] = new Vector3(6f * down, 0, 0);
            _act[(int)AthleteBone.Neck] = new Vector3(-40f * down, 0, 0);
            _act[(int)AthleteBone.LUpperArm] = new Vector3(-80f * down, 0, -30f * down);
            _act[(int)AthleteBone.RUpperArm] = new Vector3(-70f * down, 0, 35f * down);
            _act[(int)AthleteBone.LForeArm] = new Vector3(-60f * down, 0, 0);
            _act[(int)AthleteBone.RForeArm] = new Vector3(-50f * down, 0, 0);
            _act[(int)AthleteBone.LThigh] = new Vector3(-10f * down, 0, 0);
            _act[(int)AthleteBone.RShin] = new Vector3(40f * down, 0, 0);
        }

        private void Celebrate(float time)
        {
            float wave = Mathf.Sin(time * 7f);
            _act[(int)AthleteBone.LUpperArm] = new Vector3(-20f, 0, -150f + wave * 8f);
            _act[(int)AthleteBone.RUpperArm] = new Vector3(-20f, 0, 150f - wave * 8f);
            _act[(int)AthleteBone.LForeArm] = new Vector3(-25f, 0, 0);
            _act[(int)AthleteBone.RForeArm] = new Vector3(-25f, 0, 0);
            _act[(int)AthleteBone.Chest] = new Vector3(-10f, 0, 0);
            _act[(int)AthleteBone.Neck] = new Vector3(-15f, 0, 0);
        }

        private void Dejected()
        {
            _act[(int)AthleteBone.LUpperArm] = new Vector3(-120f, 0, -55f);
            _act[(int)AthleteBone.RUpperArm] = new Vector3(-120f, 0, 55f);
            _act[(int)AthleteBone.LForeArm] = new Vector3(-125f, 25f, 0);
            _act[(int)AthleteBone.RForeArm] = new Vector3(-125f, -25f, 0);
            _act[(int)AthleteBone.Neck] = new Vector3(22f, 0, 0);
            _act[(int)AthleteBone.Head] = new Vector3(12f, 0, 0);
            _act[(int)AthleteBone.Spine] = new Vector3(6f, 0, 0);
        }

        private void StepOver(float p, int side)
        {
            int k = side >= 0 ? 1 : -1;
            int leg = k > 0 ? (int)AthleteBone.RThigh : (int)AthleteBone.LThigh;
            float arc = Mathf.Sin(p * Mathf.PI);
            _act[leg] = new Vector3(-35f * arc, 0, -k * 28f * Mathf.Sin(p * Mathf.PI * 2f));
            _act[leg + 1] = new Vector3(55f * arc, 0, 0);
            _act[(int)AthleteBone.Hips] = new Vector3(6f, k * 15f * arc, k * 10f * arc);
            _act[(int)AthleteBone.Chest] = new Vector3(8f, -k * 12f * arc, 0);
            _act[(int)AthleteBone.LUpperArm] += new Vector3(0, 0, -30f * arc);
            _act[(int)AthleteBone.RUpperArm] += new Vector3(0, 0, 30f * arc);
        }
    }
}
