using System.Collections.Generic;
using NextXI.Core.Data;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Colours for one athlete (kit + appearance).</summary>
    public struct AthleteLook
    {
        public Color Shirt, Shorts, Socks, Boots, Skin, Hair, Number;
        public int ShirtNumber;

        private static readonly Color[] SkinPalette =
        {
            new Color(0.96f, 0.82f, 0.72f), new Color(0.9f, 0.72f, 0.58f), new Color(0.8f, 0.6f, 0.45f),
            new Color(0.66f, 0.46f, 0.32f), new Color(0.5f, 0.33f, 0.22f), new Color(0.36f, 0.23f, 0.15f), new Color(0.25f, 0.16f, 0.11f)
        };

        private static readonly Color[] HairPalette =
        {
            new Color(0.06f, 0.05f, 0.04f), new Color(0.16f, 0.1f, 0.06f), new Color(0.32f, 0.2f, 0.11f),
            new Color(0.52f, 0.36f, 0.2f), new Color(0.72f, 0.56f, 0.32f), new Color(0.55f, 0.24f, 0.12f), new Color(0.75f, 0.73f, 0.7f)
        };

        private static readonly Color[] BootPalette =
        {
            new Color(0.08f, 0.08f, 0.09f), new Color(0.95f, 0.95f, 0.95f), new Color(1f, 0.42f, 0.1f),
            new Color(0.1f, 0.45f, 0.95f), new Color(0.9f, 0.85f, 0.1f), new Color(0.85f, 0.1f, 0.35f)
        };

        public static Color Palette(Color[] p, float t)
        {
            t = Mathf.Clamp01(t) * (p.Length - 1);
            int i = Mathf.Min(p.Length - 2, (int)t);
            return Color.Lerp(p[i], p[i + 1], t - i);
        }

        public static AthleteLook From(PlayerProfile p, NextXI.Core.Match.KitColors kit, bool keeper)
        {
            var a = p.Appearance ?? new Appearance();
            Color shirt = Conv.Hex(keeper ? kit.Keeper : kit.Shirt, Color.red);
            return new AthleteLook
            {
                Shirt = shirt,
                Shorts = keeper ? shirt * 0.6f + Color.black * 0.4f : Conv.Hex(kit.Shorts, Color.white),
                Socks = keeper ? shirt : Conv.Hex(kit.Socks, Color.red),
                Boots = BootPalette[Mathf.Clamp(a.BootStyle, 0, BootPalette.Length - 1)],
                Skin = Palette(SkinPalette, a.SkinTone),
                Hair = Palette(HairPalette, a.HairColor),
                Number = Conv.Hex(kit.Number, Color.white),
                ShirtNumber = p.ShirtNumber
            };
        }
    }

    /// <summary>
    /// Builds a skinned athlete from a profile: bone hierarchy, SkinnedMeshRenderer with
    /// kit/skin materials, shirt and shorts numbers, and applies AthletePose each frame.
    /// The bone layout matches Unity's humanoid structure so a licensed rigged model with
    /// Mecanim clips can later replace this procedural body (see Docs/ASSETS.md).
    /// </summary>
    public sealed class AthleteRig : MonoBehaviour
    {
        public Transform[] Bones { get; private set; }
        public AthleteBody Body { get; private set; }
        public AthletePose Pose { get; private set; }
        public SkinnedMeshRenderer Renderer { get; private set; }
        private Vector3 _hipsBind;
        private static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, AthleteBody> BodyCache = new Dictionary<string, AthleteBody>();

        public static AthleteRig Create(PlayerProfile profile, AthleteLook look, Transform parent, bool shadows)
        {
            var go = new GameObject("Athlete_" + profile.ShortName);
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<AthleteRig>();
            rig.Build(profile, look, shadows);
            return rig;
        }

        private void Build(PlayerProfile profile, AthleteLook look, bool shadows)
        {
            var bp = BodyParams.From(profile);
            string key = $"{bp.Height:0.00}_{bp.Build:0.00}_{bp.FaceWidth:0.0}_{bp.FaceLength:0.0}_{bp.Hair}_{bp.Beard}_{bp.LongSleeves}_{bp.Goalkeeper}";
            if (!BodyCache.TryGetValue(key, out var body))
            {
                body = AthleteBody.Generate(bp);
                BodyCache[key] = body;
            }
            Body = body;
            int n = (int)AthleteBone.Count;
            Bones = new Transform[n];
            for (int b = 0; b < n; b++)
            {
                var t = new GameObject(((AthleteBone)b).ToString()).transform;
                Bones[b] = t;
            }
            for (int b = 0; b < n; b++)
            {
                int parent = body.Parents[b];
                var t = Bones[b];
                t.SetParent(parent < 0 ? transform : Bones[parent], false);
                Vector3 local = parent < 0 ? body.BindPositions[b] : body.BindPositions[b] - body.BindPositions[parent];
                t.localPosition = local;
                t.localRotation = Quaternion.identity;
            }
            _hipsBind = Bones[(int)AthleteBone.Hips].localPosition;

            if (!MeshCache.TryGetValue(key, out var mesh) || mesh == null)
            {
                mesh = body.Mesh.Build("Athlete_" + key);
                var bind = new Matrix4x4[n];
                for (int b = 0; b < n; b++) bind[b] = Matrix4x4.Translate(-body.BindPositions[b]);
                mesh.bindposes = bind;
                MeshCache[key] = mesh;
            }

            var smrGo = new GameObject("Body");
            smrGo.transform.SetParent(transform, false);
            var smr = smrGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = Bones;
            smr.rootBone = Bones[(int)AthleteBone.Hips];
            smr.quality = SkinQuality.Bone2;
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(new Vector3(0, -0.1f, 0), new Vector3(2.4f, 2.6f, 2.4f));
            smr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            smr.sharedMaterials = Materials(look);
            Renderer = smr;
            Pose = new AthletePose(body.Pos(AthleteBone.Hips).y);

            AddNumber(look, Bones[(int)AthleteBone.Chest], new Vector3(0, 0.08f, -0.118f), 0.26f, true);
            AddNumber(look, Bones[(int)AthleteBone.Hips], new Vector3(0.1f, -0.05f, 0.112f), 0.085f, false);
        }

        private static Material[] Materials(AthleteLook l)
        {
            string K(Color c) => ColorUtility.ToHtmlStringRGB(c);
            var skin = Mat.Opaque("skin" + K(l.Skin), l.Skin, 0.38f, 0f, ProcTex.SkinDetail());
            var shirt = Mat.Opaque("shirt" + K(l.Shirt), l.Shirt, 0.28f, 0f, ProcTex.Fabric());
            var shorts = Mat.Opaque("shorts" + K(l.Shorts), l.Shorts, 0.28f, 0f, ProcTex.Fabric());
            var socks = Mat.Opaque("socks" + K(l.Socks), l.Socks, 0.2f, 0f, ProcTex.Fabric());
            var boots = Mat.Opaque("boots" + K(l.Boots), l.Boots, 0.62f, 0.1f);
            var hair = Mat.Opaque("hair" + K(l.Hair), l.Hair, 0.22f);
            var eyes = Mat.Opaque("eyes", new Color(0.06f, 0.05f, 0.05f), 0.8f);
            return new[] { skin, shirt, shorts, socks, boots, hair, eyes };
        }

        /// <summary>Number decal: a cutout quad following the bone (back of shirt, front of shorts).</summary>
        private static void AddNumber(AthleteLook l, Transform bone, Vector3 localPos, float size, bool back)
        {
            if (l.ShirtNumber <= 0) return;
            var tex = ProcTex.Number(l.ShirtNumber, l.Number, l.Shirt * 0.45f + Color.black * 0.3f);
            var mat = Mat.Cutout("num" + l.ShirtNumber + ColorUtility.ToHtmlStringRGB(l.Number) + ColorUtility.ToHtmlStringRGB(l.Shirt), tex, Color.white, false);
            var b = new MeshBuilder();
            Vector3 n = back ? Vector3.back : Vector3.forward;
            // Facing the outside of the body: for the back decal the visible side is -Z.
            // Viewer-right of the visible side (seen from behind, viewer right is +X).
            Vector3 right = back ? Vector3.right : Vector3.left;
            float h = size * 0.5f;
            int i0 = b.Add(-right * h - Vector3.up * h, n, new Vector2(0, 0));
            int i1 = b.Add(right * h - Vector3.up * h, n, new Vector2(1, 0));
            int i2 = b.Add(right * h + Vector3.up * h, n, new Vector2(1, 1));
            int i3 = b.Add(-right * h + Vector3.up * h, n, new Vector2(0, 1));
            b.Tri(0, i0, i2, i1); b.Tri(0, i0, i3, i2);
            var go = MeshKitUnity.Spawn(back ? "NumberBack" : "NumberShorts", b.Build("Number"), new[] { mat }, bone, false, true);
            go.transform.localPosition = localPos;
        }

        /// <summary>Applies the solved pose to the bones.</summary>
        public void ApplyPose()
        {
            var e = Pose.Euler;
            for (int b = 1; b < Bones.Length; b++) Bones[b].localRotation = Quaternion.Euler(e[b]);
            Bones[(int)AthleteBone.Hips].localPosition = _hipsBind + Pose.HipsOffset;
        }

        public Vector3 HandPosition(bool right) => Bones[(int)(right ? AthleteBone.RHand : AthleteBone.LHand)].position;
        public Vector3 HeadPosition => Bones[(int)AthleteBone.Head].position;

        public void SetShadows(bool on) =>
            Renderer.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
