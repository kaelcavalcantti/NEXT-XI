using System.Collections.Generic;
using NextXI.Core.Settings;
using NextXI.Runtime.AudioSynth;
using AudioSettings = NextXI.Core.Settings.AudioSettings;
using UnityEngine;

namespace NextXI.Runtime
{
    public enum AudioCategory { Music, Ambience, Crowd, Effects, Commentary, Interface }
    public enum UiSound { Click, Confirm, Back }
    public enum Sfx { KickSoft, KickHard, Whistle, WhistleLong, WhistleTriple, Net, Post, Tackle, GoalRoar, NearMiss, Boo, Applause, Chant }

    /// <summary>
    /// Audio system: procedurally synthesised clips (Synth), pooled one-shot sources with 3D
    /// positioning and random pitch/volume variation (no obvious repetition), looping music
    /// and stadium beds, and per-category volumes (music, ambience, crowd, effects,
    /// commentary, interface) with master volume and mute, persisted in GameSettings.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }

        private AudioSettings _settings = new AudioSettings();
        private readonly Dictionary<Sfx, AudioClip[]> _sfx = new Dictionary<Sfx, AudioClip[]>();
        private readonly Dictionary<UiSound, AudioClip> _ui = new Dictionary<UiSound, AudioClip>();
        private AudioClip _music;
        private AudioClip[] _crowdBeds;
        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private AudioSource _musicSrc, _uiSrc;
        private readonly List<AudioSource> _crowdSrc = new List<AudioSource>();
        private AudioSource _chantSrc;
        private float _musicTarget, _crowdTarget, _crowdPitch = 1f;
        private int _poolIdx;
        public bool Ready { get; private set; }

        public static AudioService Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("NX_Audio");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AudioService>();
            Instance.Init();
            return Instance;
        }

        private void Init()
        {
            for (int i = 0; i < 16; i++)
            {
                var s = new GameObject("Sfx" + i).AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.playOnAwake = false;
                s.spatialBlend = 0.85f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 8f;
                s.maxDistance = 140f;
                s.dopplerLevel = 0f;
                _pool.Add(s);
            }
            _musicSrc = gameObject.AddComponent<AudioSource>();
            _musicSrc.loop = true; _musicSrc.spatialBlend = 0f; _musicSrc.playOnAwake = false;
            _uiSrc = gameObject.AddComponent<AudioSource>();
            _uiSrc.spatialBlend = 0f; _uiSrc.playOnAwake = false;
            _chantSrc = gameObject.AddComponent<AudioSource>();
            _chantSrc.spatialBlend = 0f; _chantSrc.playOnAwake = false;
            BuildClips();
        }

        private static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Synth.Rate, false);
            c.SetData(data, 0);
            return c;
        }

        private void BuildClips()
        {
            // Variations avoid machine-gun repetition.
            _sfx[Sfx.KickSoft] = new[] { Clip("kick_s1", Synth.Kick(0.25f, 1)), Clip("kick_s2", Synth.Kick(0.35f, 2)), Clip("kick_s3", Synth.Kick(0.3f, 3)) };
            _sfx[Sfx.KickHard] = new[] { Clip("kick_h1", Synth.Kick(0.85f, 4)), Clip("kick_h2", Synth.Kick(1f, 5)) };
            _sfx[Sfx.Whistle] = new[] { Clip("whistle", Synth.Whistle(0.35f, 6)) };
            _sfx[Sfx.WhistleLong] = new[] { Clip("whistle_long", Synth.Whistle(0.9f, 7)) };
            _sfx[Sfx.WhistleTriple] = new[] { Clip("whistle3", Synth.WhistleTriple(8)) };
            _sfx[Sfx.Net] = new[] { Clip("net", Synth.Net(9)) };
            _sfx[Sfx.Post] = new[] { Clip("post", Synth.Post(10)) };
            _sfx[Sfx.Tackle] = new[] { Clip("tackle1", Synth.Tackle(11)), Clip("tackle2", Synth.Tackle(12)) };
            _sfx[Sfx.GoalRoar] = new[] { Clip("roar1", Synth.GoalRoar(13)), Clip("roar2", Synth.GoalRoar(14)) };
            _sfx[Sfx.NearMiss] = new[] { Clip("ohh1", Synth.NearMiss(15)), Clip("ohh2", Synth.NearMiss(16)) };
            _sfx[Sfx.Boo] = new[] { Clip("boo", Synth.Boo(17)) };
            _sfx[Sfx.Applause] = new[] { Clip("applause", Synth.Applause(3f, 18)) };
            _sfx[Sfx.Chant] = new[] { Clip("chant1", Synth.Chant(8f, 19, 196f)), Clip("chant2", Synth.Chant(8f, 20, 174.6f)) };
            _ui[UiSound.Click] = Clip("ui_click", Synth.UiClick(1f));
            _ui[UiSound.Back] = Clip("ui_back", Synth.UiClick(0.7f));
            _ui[UiSound.Confirm] = Clip("ui_confirm", Synth.UiConfirm());
            _music = Clip("menu_theme", Synth.MenuMusic(1));
            _crowdBeds = new[] { Clip("crowd_calm", Synth.CrowdBed(8f, 21, 0.3f)), Clip("crowd_hot", Synth.CrowdBed(8f, 22, 1f)) };
            Ready = true;
        }

        public void Apply(AudioSettings s)
        {
            _settings = s ?? new AudioSettings();
            AudioListener.volume = _settings.Muted ? 0f : _settings.Master;
        }

        public float Volume(AudioCategory c)
        {
            switch (c)
            {
                case AudioCategory.Music: return _settings.Music;
                case AudioCategory.Ambience: return _settings.Ambience;
                case AudioCategory.Crowd: return _settings.Crowd;
                case AudioCategory.Effects: return _settings.Effects;
                case AudioCategory.Commentary: return _settings.Commentary;
                default: return _settings.Interface;
            }
        }

        private static AudioCategory CategoryOf(Sfx s)
        {
            switch (s)
            {
                case Sfx.GoalRoar: case Sfx.NearMiss: case Sfx.Boo: case Sfx.Applause: case Sfx.Chant: return AudioCategory.Crowd;
                default: return AudioCategory.Effects;
            }
        }

        public void Play(Sfx s, Vector3? at = null, float volume = 1f, float pitchVar = 0.06f)
        {
            if (!Ready || !_sfx.TryGetValue(s, out var clips)) return;
            var src = _pool[_poolIdx];
            _poolIdx = (_poolIdx + 1) % _pool.Count;
            src.clip = clips[Random.Range(0, clips.Length)];
            src.pitch = 1f + Random.Range(-pitchVar, pitchVar);
            src.volume = Mathf.Clamp01(volume * Random.Range(0.9f, 1f) * Volume(CategoryOf(s)));
            src.spatialBlend = at.HasValue ? 0.85f : 0f;
            if (at.HasValue) src.transform.position = at.Value;
            src.Play();
        }

        public void PlayUi(UiSound s)
        {
            if (!Ready) return;
            _uiSrc.PlayOneShot(_ui[s], Volume(AudioCategory.Interface));
        }

        // ------------------------------------------------------------------ music & crowd

        public void PlayMusic(bool on)
        {
            if (!Ready) return;
            _musicTarget = on ? 1f : 0f;
            if (on && !_musicSrc.isPlaying) { _musicSrc.clip = _music; _musicSrc.volume = 0f; _musicSrc.Play(); }
        }

        /// <summary>Starts the stadium beds positioned around the stands.</summary>
        public void StartCrowd(Vector3[] standPositions)
        {
            StopCrowd();
            for (int i = 0; i < standPositions.Length; i++)
            {
                var s = new GameObject("CrowdBed" + i).AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.transform.position = standPositions[i];
                s.clip = _crowdBeds[i % 2];
                s.loop = true;
                s.spatialBlend = 0.6f;
                s.minDistance = 30f; s.maxDistance = 300f;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.timeSamples = Random.Range(0, s.clip.samples);
                s.volume = 0f;
                s.Play();
                _crowdSrc.Add(s);
            }
        }

        public void StopCrowd()
        {
            foreach (var s in _crowdSrc) if (s != null) Destroy(s.gameObject);
            _crowdSrc.Clear();
            _chantSrc.Stop();
        }

        /// <summary>Crowd excitement 0..1 (drives bed level, brightness and chants).</summary>
        public void SetCrowdExcitement(float e)
        {
            _crowdTarget = Mathf.Clamp01(e);
            _crowdPitch = Mathf.Lerp(0.96f, 1.06f, e);
        }

        public void Chant()
        {
            if (!Ready || _chantSrc.isPlaying) return;
            var clips = _sfx[Sfx.Chant];
            _chantSrc.clip = clips[Random.Range(0, clips.Length)];
            _chantSrc.volume = 0.55f * Volume(AudioCategory.Crowd);
            _chantSrc.pitch = Random.Range(0.97f, 1.03f);
            _chantSrc.Play();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_musicSrc != null)
            {
                _musicSrc.volume = Mathf.MoveTowards(_musicSrc.volume, _musicTarget * 0.8f * Volume(AudioCategory.Music), dt * 0.6f);
                if (_musicTarget <= 0f && _musicSrc.volume <= 0.001f && _musicSrc.isPlaying) _musicSrc.Stop();
            }
            for (int i = 0; i < _crowdSrc.Count; i++)
            {
                var s = _crowdSrc[i];
                bool hot = i % 2 == 1;
                float level = hot ? _crowdTarget : 1f - _crowdTarget * 0.6f;
                float target = (0.25f + 0.55f * level) * Volume(AudioCategory.Crowd) * Volume(AudioCategory.Ambience);
                s.volume = Mathf.MoveTowards(s.volume, target, dt * 0.5f);
                s.pitch = Mathf.MoveTowards(s.pitch, _crowdPitch, dt * 0.1f);
            }
        }
    }
}
