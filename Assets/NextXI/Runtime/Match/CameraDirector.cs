using NextXI.Core.Match;
using NextXI.Core.Settings;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>
    /// Match cameras: wide/close broadcast (side stand, tracking the ball along the length),
    /// player cam (behind the controlled athlete, oriented to the attacking direction),
    /// dynamic (broadcast that tightens near goal), set-piece and replay cameras.
    /// All motion is critically damped to avoid jitter and sudden jumps.
    /// </summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public CameraSettings Settings = new CameraSettings();
        public CameraMode Mode = CameraMode.Broadcast;
        private Vector3 _pos, _vel, _look, _lookVel;
        private float _fov, _fovVel;
        private bool _snap = true;
        private int _replayAngle;

        public static CameraDirector Create(Transform parent)
        {
            var go = new GameObject("MatchCamera");
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.05f, 0.07f, 0.12f);
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
            var d = go.AddComponent<CameraDirector>();
            d.Cam = cam;
            return d;
        }

        public void Snap() => _snap = true;

        /// <summary>Gameplay camera. `focus` = ball, `user` = controlled athlete, `attackDir` = +1/-1 along X.</summary>
        public void UpdateGameplay(Vector3 ball, Vector3 ballVel, Vector3? user, int attackDir, bool setPiece, float dt)
        {
            var mode = setPiece && Settings.SetPieceMode != Mode ? Settings.SetPieceMode : Mode;
            Vector3 targetPos, targetLook;
            float fov;
            float h = Mathf.Lerp(0.7f, 1.4f, Settings.Height);
            float dist = Mathf.Lerp(0.75f, 1.35f, Settings.Distance);
            float zoom = Mathf.Lerp(1.25f, 0.8f, Settings.Zoom);
            Vector3 focus = new Vector3(ball.x, Mathf.Min(ball.y, 3f) * 0.4f, ball.z) + new Vector3(ballVel.x, 0, ballVel.z) * 0.35f;
            switch (mode)
            {
                case CameraMode.PlayerCam:
                {
                    Vector3 u = user ?? ball;
                    Vector3 fwd = new Vector3(attackDir, 0, 0);
                    Vector3 mid = Vector3.Lerp(u, focus, 0.45f);
                    targetPos = mid - fwd * (13f * dist) + Vector3.up * (6.5f * h);
                    targetLook = mid + fwd * 6f + Vector3.up * 0.8f;
                    fov = 52f * zoom;
                    break;
                }
                case CameraMode.BroadcastClose:
                    targetPos = new Vector3(focus.x * 0.92f, 13f * h, -34f - 22f * dist);
                    targetLook = new Vector3(focus.x, 0.5f, focus.z * 0.7f);
                    fov = 27f * zoom;
                    break;
                case CameraMode.Dynamic:
                {
                    float nearGoal = Mathf.InverseLerp(30f, 50f, Mathf.Abs(focus.x));
                    targetPos = new Vector3(focus.x * Mathf.Lerp(0.85f, 0.7f, nearGoal), Mathf.Lerp(18f, 12f, nearGoal) * h, -38f - Mathf.Lerp(26f, 14f, nearGoal) * dist);
                    targetLook = new Vector3(focus.x, 0.5f, focus.z * 0.75f);
                    fov = Mathf.Lerp(34f, 30f, nearGoal) * zoom;
                    break;
                }
                default: // Broadcast
                    targetPos = new Vector3(focus.x * 0.85f, 21f * h, -40f - 34f * dist);
                    targetLook = new Vector3(focus.x, 0f, focus.z * 0.65f);
                    fov = 34f * zoom;
                    break;
            }
            targetLook.y += Mathf.Lerp(-3f, 3f, Settings.Angle) * (mode == CameraMode.PlayerCam ? 0.3f : 1f);
            Move(targetPos, targetLook, fov, dt, Mathf.Lerp(0.55f, 0.18f, Settings.Smoothing));
        }

        /// <summary>Cinematic replay camera alternating angles around the action.</summary>
        public void UpdateReplay(Vector3 ball, float t, float dt, int goalSide)
        {
            Vector3 pos;
            switch (_replayAngle % 3)
            {
                case 0: pos = new Vector3(goalSide * 62f, 4.5f, ball.z * 0.4f + 8f); break;     // behind the goal
                case 1: pos = ball + new Vector3(-goalSide * 9f, 3.2f, -10f); break;           // low tracking
                default: pos = new Vector3(ball.x - goalSide * 4f, 30f, -24f); break;          // high
            }
            Move(pos, ball + Vector3.up * 0.6f, _replayAngle % 3 == 1 ? 40f : 28f, dt, 0.25f);
        }

        public void NextReplayAngle() { _replayAngle++; _snap = true; }

        /// <summary>Slow orbit used by the main menu and pre-match presentation.</summary>
        public void UpdateOrbit(float time, float dt, float radius = 78f, float height = 26f)
        {
            float a = time * 0.06f;
            var pos = new Vector3(Mathf.Cos(a) * radius, height + Mathf.Sin(time * 0.1f) * 4f, Mathf.Sin(a) * radius * 0.75f);
            Move(pos, new Vector3(0, 2f, 0), 42f, dt, 0.8f);
        }

        /// <summary>Close-up of a player (celebrations, presentations).</summary>
        public void UpdateCloseUp(Vector3 subject, Vector3 facingDir, float dt)
        {
            var pos = subject + facingDir.normalized * 4.2f + Vector3.up * 1.7f;
            Move(pos, subject + Vector3.up * 1.4f, 38f, dt, 0.35f);
        }

        private void Move(Vector3 pos, Vector3 look, float fov, float dt, float smooth)
        {
            if (_snap || dt <= 0f)
            {
                _pos = pos; _look = look; _fov = fov; _vel = _lookVel = Vector3.zero; _fovVel = 0f;
                _snap = false;
            }
            else
            {
                _pos = Vector3.SmoothDamp(_pos, pos, ref _vel, smooth, 80f, dt);
                _look = Vector3.SmoothDamp(_look, look, ref _lookVel, smooth * 0.7f, 120f, dt);
                _fov = Mathf.SmoothDamp(_fov, fov, ref _fovVel, smooth, 200f, dt);
            }
            transform.position = _pos;
            transform.rotation = Quaternion.LookRotation(_look - _pos, Vector3.up);
            Cam.fieldOfView = _fov;
        }

        /// <summary>Camera-relative stick to world XZ (so "up" on the stick is "away from camera").</summary>
        public Vector3 StickToWorld(Vector2 stick)
        {
            Vector3 f = transform.forward; f.y = 0; f.Normalize();
            Vector3 r = transform.right; r.y = 0; r.Normalize();
            return f * stick.y + r * stick.x;
        }
    }
}
