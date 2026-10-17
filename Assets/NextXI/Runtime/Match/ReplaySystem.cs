using System.Collections.Generic;
using NextXI.Core.Match;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>One recorded instant of the whole scene.</summary>
    public sealed class ReplayFrame
    {
        public float Time;
        public Vector3 BallPos, BallVel, BallSpin;
        public readonly Dictionary<int, PlayerFrame> Players = new Dictionary<int, PlayerFrame>(24);
    }

    /// <summary>
    /// Ring buffer of the last seconds of play at 30 Hz (light: kinematic state only; poses are
    /// re-solved on playback). Supports goal replays and highlights.
    /// </summary>
    public sealed class ReplaySystem
    {
        public const float Rate = 30f;
        private readonly ReplayFrame[] _frames;
        private int _head, _count;
        private float _accum;

        public ReplaySystem(float seconds = 12f)
        {
            _frames = new ReplayFrame[Mathf.CeilToInt(seconds * Rate)];
            for (int i = 0; i < _frames.Length; i++) _frames[i] = new ReplayFrame();
        }

        public int Count => _count;

        public void Record(MatchEngine m, float time, float dt)
        {
            _accum += dt;
            if (_accum < 1f / Rate) return;
            _accum = 0f;
            var f = _frames[_head];
            f.Time = time;
            f.BallPos = m.Ball.Position.ToUnity();
            f.BallVel = m.Ball.Velocity.ToUnity();
            f.BallSpin = m.Ball.Spin.ToUnity();
            f.Players.Clear();
            foreach (var p in m.Players) f.Players[p.Id] = PlayerFrame.From(m, p);
            _head = (_head + 1) % _frames.Length;
            if (_count < _frames.Length) _count++;
        }

        public void Clear() { _count = 0; _head = 0; }

        /// <summary>Frames from `secondsBack` before the newest one, oldest first.</summary>
        public List<ReplayFrame> Extract(float secondsBack, float trimEnd = 0f)
        {
            var list = new List<ReplayFrame>();
            int n = Mathf.Min(_count, Mathf.CeilToInt(secondsBack * Rate));
            int skipEnd = Mathf.Min(n, Mathf.CeilToInt(trimEnd * Rate));
            for (int i = n - 1; i >= skipEnd; i--)
            {
                int idx = ((_head - 1 - i) % _frames.Length + _frames.Length) % _frames.Length;
                var src = _frames[idx];
                var copy = new ReplayFrame { Time = src.Time, BallPos = src.BallPos, BallVel = src.BallVel, BallSpin = src.BallSpin };
                foreach (var kv in src.Players) copy.Players[kv.Key] = kv.Value;
                list.Add(copy);
            }
            return list;
        }
    }

    /// <summary>Plays back extracted frames with time interpolation.</summary>
    public sealed class ReplayPlayer
    {
        private List<ReplayFrame> _frames;
        public float Time { get; private set; }
        public float Duration => _frames == null || _frames.Count < 2 ? 0f : (_frames.Count - 1) / ReplaySystem.Rate;
        public bool Active => _frames != null;
        public float Speed = 0.75f;

        public void Start(List<ReplayFrame> frames)
        {
            _frames = frames.Count >= 2 ? frames : null;
            Time = 0f;
        }

        public void Stop() => _frames = null;

        /// <summary>Advances and returns false when finished.</summary>
        public bool Step(float dt) { Time += dt * Speed; return Time < Duration; }

        public void Sample(out ReplayFrame a, out ReplayFrame b, out float t)
        {
            float x = Time * ReplaySystem.Rate;
            int i = Mathf.Clamp(Mathf.FloorToInt(x), 0, _frames.Count - 2);
            a = _frames[i];
            b = _frames[i + 1];
            t = Mathf.Clamp01(x - i);
        }
    }
}
