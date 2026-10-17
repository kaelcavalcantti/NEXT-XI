using NextXI.Core;
using NextXI.Core.Data;
using NextXI.Core.Match;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Snapshot of one player for rendering and replays (recorded at 30 Hz).</summary>
    public struct PlayerFrame
    {
        public Vector3 Pos, Vel;
        public float Facing, JumpHeight, ActionTime, ActionDuration, ContactTime;
        public PlayerAction Action;
        public int KickSide, DiveSide;
        public bool HasBall, Jockey, Shield, Active;
        public float Fatigue, KickPower;

        public static PlayerFrame From(MatchEngine m, MatchPlayer p)
        {
            int kickSide = (p.Profile.PreferredFoot == Foot.Right) ^ p.KickWithWeakFoot ? 1 : -1;
            int diveSide = 1;
            if (p.Action == PlayerAction.KeeperDive || p.Action == PlayerAction.Slide)
            {
                Vec3 right = p.RightDir;
                diveSide = Vec3.Dot(p.ActionDir, right) >= 0 ? 1 : -1;
                if (p.Action == PlayerAction.Slide) kickSide = diveSide;
            }
            return new PlayerFrame
            {
                Pos = p.Pos.ToUnity(), Vel = p.Vel.ToUnity(), Facing = p.Facing, JumpHeight = p.JumpHeight,
                Action = p.Action, ActionTime = p.ActionTime, ActionDuration = p.ActionDuration,
                ContactTime = p.ContactTime >= 90f ? 0f : p.ContactTime,
                KickSide = kickSide, DiveSide = diveSide, HasBall = m.Owner == p, Jockey = p.Jockeying, Shield = p.Shielding,
                Active = p.IsActive, Fatigue = 1f - p.Stamina, KickPower = p.Kick.Power
            };
        }

        public static PlayerFrame Lerp(PlayerFrame a, PlayerFrame b, float t)
        {
            var r = t < 0.5f ? a : b;
            r.Pos = Vector3.Lerp(a.Pos, b.Pos, t);
            r.Vel = Vector3.Lerp(a.Vel, b.Vel, t);
            r.Facing = a.Facing + Mathf.DeltaAngle(a.Facing * Mathf.Rad2Deg, b.Facing * Mathf.Rad2Deg) * Mathf.Deg2Rad * t;
            r.JumpHeight = Mathf.Lerp(a.JumpHeight, b.JumpHeight, t);
            if (a.Action == b.Action) r.ActionTime = Mathf.Lerp(a.ActionTime, b.ActionTime, t);
            return r;
        }
    }

    /// <summary>Visual representation of a MatchPlayer: rig, animation, user marker.</summary>
    public sealed class PlayerView : MonoBehaviour
    {
        public int PlayerId { get; private set; }
        public AthleteRig Rig { get; private set; }
        private Transform _marker;
        private Renderer _markerRenderer;
        private float _lastFacing;
        private Vector3 _lastVel;
        private bool _hasLast;

        public static PlayerView Create(MatchPlayer p, KitColors kit, Transform parent, bool shadows)
        {
            var go = new GameObject($"P{p.Id}_{p.ShirtNumber}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<PlayerView>();
            v.PlayerId = p.Id;
            var look = AthleteLook.From(p.Profile, kit, p.IsGoalkeeper);
            if (look.ShirtNumber <= 0) look.ShirtNumber = p.ShirtNumber;
            v.Rig = AthleteRig.Create(p.Profile, look, go.transform, shadows);
            v.CreateMarker();
            return v;
        }

        private void CreateMarker()
        {
            var b = new MeshBuilder();
            MeshKit.FlatQuad(b, 0, new Vector3(0, 0, -0.75f), new Vector3(0, 0, 0.75f), 1.5f, 0.02f);
            var mat = Mat.Cutout("marker", ProcTex.Ring(false), new Color(1f, 0.55f, 0.1f));
            Mat.SetEmission(mat, new Color(0.8f, 0.4f, 0.05f));
            var go = MeshKitUnity.Spawn("UserMarker", b.Build("Marker"), new[] { mat }, transform, false, false);
            _marker = go.transform;
            _markerRenderer = go.GetComponent<Renderer>();
            _marker.gameObject.SetActive(false);
        }

        public void SetUserMarker(bool on, bool hasBall)
        {
            if (_marker.gameObject.activeSelf != on) _marker.gameObject.SetActive(on);
            if (on) _marker.localScale = Vector3.one * (hasBall ? 1.1f : 0.9f);
        }

        /// <summary>Drives transform and animation from a frame. `dt` is the render delta.</summary>
        public void Apply(in PlayerFrame f, float dt, float time, Vector3 ballPos)
        {
            if (gameObject.activeSelf != f.Active) gameObject.SetActive(f.Active);
            if (!f.Active) return;
            transform.position = f.Pos;
            transform.rotation = Conv.FacingToRotation(f.Facing);
            if (dt <= 0f) return;

            Vector3 localVel = Quaternion.Inverse(transform.rotation) * f.Vel;
            float turnRate = 0f, accel = 0f;
            if (_hasLast)
            {
                // Facing is atan2(z,x): counter-clockwise from above, i.e. a positive delta turns left.
                turnRate = -Mathf.DeltaAngle(_lastFacing * Mathf.Rad2Deg, f.Facing * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt;
                accel = (f.Vel.magnitude - _lastVel.magnitude) / dt;
            }
            _lastFacing = f.Facing; _lastVel = f.Vel; _hasLast = true;

            Vector3 toBall = Quaternion.Inverse(transform.rotation) * (ballPos - f.Pos);
            float look = Mathf.Clamp(Mathf.Atan2(toBall.x, toBall.z) * Mathf.Rad2Deg, -70f, 70f);
            var input = new AnimInput
            {
                LocalVelocity = localVel,
                Accel = Mathf.Clamp(accel, -10f, 10f),
                TurnRate = Mathf.Clamp(turnRate, -6f, 6f),
                Action = f.Action,
                ActionTime = f.ActionTime,
                ActionDuration = f.ActionDuration,
                ContactTime = f.ContactTime,
                KickSide = f.KickSide,
                DiveSide = f.DiveSide,
                JumpHeight = f.JumpHeight,
                HasBall = f.HasBall,
                Jockey = f.Jockey,
                Shield = f.Shield,
                Goalkeeper = PlayerIdIsKeeper,
                Fatigue = f.Fatigue,
                Time = time + PlayerId * 0.37f,
                LookYaw = look,
                KickPower = f.KickPower
            };
            Rig.Pose.Update(input, dt);
            Rig.ApplyPose();
            // Jumps lift the whole body.
            Rig.transform.localPosition = new Vector3(0, f.JumpHeight, 0);
        }

        public bool PlayerIdIsKeeper;
    }

    /// <summary>The ball: mesh with panel texture, spin-driven rotation and a contact shadow.</summary>
    public sealed class BallView : MonoBehaviour
    {
        private Transform _mesh;
        private Transform _shadow;

        public static BallView Create(Transform parent)
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<BallView>();
            var b = new MeshBuilder();
            MeshKit.Sphere(b, 0, Vector3.zero, Vector3.one * BallPhysics.Radius, 28, 16);
            var mesh = MeshKitUnity.Spawn("BallMesh", b.Build("Ball"), new[] { Mat.Opaque("ball", Color.white, 0.55f, 0f, ProcTex.Ball()) }, go.transform, true, false);
            v._mesh = mesh.transform;
            var sb = new MeshBuilder();
            MeshKit.FlatQuad(sb, 0, new Vector3(0, 0, -0.18f), new Vector3(0, 0, 0.18f), 0.36f, 0.012f);
            var sm = Mat.Cutout("ballshadow", ProcTex.Ring(true), new Color(0, 0, 0, 0.6f));
            v._shadow = MeshKitUnity.Spawn("BallShadow", sb.Build("BallShadow"), new[] { sm }, parent, false, false).transform;
            return v;
        }

        public void Apply(Vector3 pos, Vector3 spin, Vector3 vel, float dt)
        {
            transform.position = pos;
            // Rolling/spinning rotation integrated from angular velocity.
            Vector3 w = spin;
            if (w.sqrMagnitude < 1e-4f && vel.sqrMagnitude > 0.01f) w = Vector3.Cross(Vector3.up, vel) / BallPhysics.Radius;
            float ang = w.magnitude * dt * Mathf.Rad2Deg;
            if (ang > 0.001f) _mesh.rotation = Quaternion.AngleAxis(ang, w.normalized) * _mesh.rotation;
            _shadow.position = new Vector3(pos.x, 0.012f, pos.z);
            float h = Mathf.Clamp01((pos.y - BallPhysics.Radius) / 6f);
            _shadow.localScale = Vector3.one * Mathf.Lerp(1f, 2.2f, h);
            _shadow.gameObject.SetActive(h < 0.95f);
        }

        public void SetVisible(bool v)
        {
            gameObject.SetActive(v);
            _shadow.gameObject.SetActive(v);
        }
    }
}
