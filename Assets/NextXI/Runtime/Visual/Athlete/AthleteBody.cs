using System;
using System.Collections.Generic;
using NextXI.Core.Data;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Bone indices of the procedural athlete skeleton (humanoid-compatible layout).</summary>
    public enum AthleteBone
    {
        Root, Hips, Spine, Chest, Neck, Head,
        LClavicle, LUpperArm, LForeArm, LHand,
        RClavicle, RUpperArm, RForeArm, RHand,
        LThigh, LShin, LFoot,
        RThigh, RShin, RFoot,
        Count
    }

    /// <summary>Submesh / material slots of the athlete mesh.</summary>
    public enum AthleteMaterial { Skin, Shirt, Shorts, Socks, Boots, Hair, Eyes, Count }

    /// <summary>Body parameters resolved from a player profile, clamped to anatomical limits.</summary>
    public struct BodyParams
    {
        public float Height;   // metres, 1.60..2.02
        public float Build;    // 0 lean .. 1 heavy
        public float FaceWidth, FaceLength;
        public HairStyle Hair;
        public BeardStyle Beard;
        public bool LongSleeves;
        public bool Goalkeeper;

        public static BodyParams From(PlayerProfile p)
        {
            var a = p.Appearance ?? new Appearance();
            a.Clamp();
            float h = Mathf.Clamp(p.HeightCm / 100f, 1.6f, 2.02f);
            // Build follows BMI so weight is respected but limited to plausible athletic shapes.
            float bmi = p.WeightKg / (h * h);
            float build = Mathf.Clamp01(Mathf.Lerp(Mathf.InverseLerp(19.5f, 26.5f, bmi), a.Build, 0.35f));
            return new BodyParams
            {
                Height = h, Build = build, FaceWidth = a.FaceWidth, FaceLength = a.FaceLength,
                Hair = a.Hair, Beard = a.Beard, LongSleeves = a.LongSleeves || p.Position == Position.GK,
                Goalkeeper = p.Position == Position.GK
            };
        }
    }

    /// <summary>
    /// Generates the athlete's skinned body mesh in bind pose (A-pose, facing +Z, feet at y=0).
    /// Proportions follow standard adult male anthropometry scaled by height; widths scale
    /// with build inside fixed limits so no customisation can produce deformed bodies.
    /// The whole body is one continuous skinned surface: joints blend between bones so
    /// knees/elbows/shoulders bend without gaps.
    /// </summary>
    public sealed class AthleteBody
    {
        public readonly Vector3[] BindPositions = new Vector3[(int)AthleteBone.Count];
        public readonly int[] Parents = new int[(int)AthleteBone.Count];
        public MeshBuilder Mesh { get; private set; }
        public BodyParams Params { get; private set; }

        private float _s;    // height scale relative to 1.80 m
        private float _w;    // width scale from build
        private const int Seg = 16;

        public static AthleteBody Generate(BodyParams p)
        {
            var b = new AthleteBody { Params = p };
            b.Build();
            return b;
        }

        private Vector3 S(float x, float y, float z) => new Vector3(x * _s * _w, y * _s, z * _s * Mathf.Lerp(0.97f, 1.06f, Params.Build));

        private void Build()
        {
            _s = Params.Height / 1.8f;
            // Build changes girth +-8% at most; tall players are proportionally slightly leaner.
            _w = Mathf.Lerp(0.93f, 1.08f, Params.Build) * Mathf.Lerp(1.02f, 0.98f, Mathf.InverseLerp(1.65f, 1.98f, Params.Height));
            Mesh = new MeshBuilder((int)AthleteMaterial.Count) { Skinned = true };
            DefineSkeleton();
            BuildLegs();
            BuildTorso();
            BuildArms();
            BuildHead();
        }

        // ------------------------------------------------------------------ skeleton

        private void Bone(AthleteBone b, AthleteBone parent, Vector3 pos)
        {
            BindPositions[(int)b] = pos;
            Parents[(int)b] = (int)parent;
        }

        private const float ArmOut = 11f; // degrees the arms hang away from the body in bind pose

        public Vector3 ArmDir(int side) => new Vector3(side * Mathf.Sin(ArmOut * Mathf.Deg2Rad), -Mathf.Cos(ArmOut * Mathf.Deg2Rad), 0);

        private void DefineSkeleton()
        {
            Bone(AthleteBone.Root, AthleteBone.Root, Vector3.zero);
            Parents[0] = -1;
            Bone(AthleteBone.Hips, AthleteBone.Root, S(0, 0.95f, 0));
            Bone(AthleteBone.Spine, AthleteBone.Hips, S(0, 1.08f, -0.01f));
            Bone(AthleteBone.Chest, AthleteBone.Spine, S(0, 1.24f, -0.005f));
            Bone(AthleteBone.Neck, AthleteBone.Chest, S(0, 1.48f, -0.02f));
            Bone(AthleteBone.Head, AthleteBone.Neck, S(0, 1.585f, 0f));
            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side < 0;
                var clav = left ? AthleteBone.LClavicle : AthleteBone.RClavicle;
                var ua = left ? AthleteBone.LUpperArm : AthleteBone.RUpperArm;
                var fa = left ? AthleteBone.LForeArm : AthleteBone.RForeArm;
                var hand = left ? AthleteBone.LHand : AthleteBone.RHand;
                Vector3 shoulder = S(side * 0.185f, 1.425f, -0.015f);
                Bone(clav, AthleteBone.Chest, S(side * 0.03f, 1.43f, 0f));
                Bone(ua, clav, shoulder);
                Vector3 elbow = shoulder + ArmDir(side) * (0.30f * _s);
                Bone(fa, ua, elbow);
                Vector3 wrist = elbow + ArmDir(side) * (0.255f * _s);
                Bone(hand, fa, wrist);

                var th = left ? AthleteBone.LThigh : AthleteBone.RThigh;
                var sh = left ? AthleteBone.LShin : AthleteBone.RShin;
                var ft = left ? AthleteBone.LFoot : AthleteBone.RFoot;
                Bone(th, AthleteBone.Hips, S(side * 0.092f, 0.915f, 0f));
                Bone(sh, th, S(side * 0.088f, 0.495f, 0.005f));
                Bone(ft, sh, S(side * 0.086f, 0.078f, -0.01f));
            }
        }

        public Vector3 Pos(AthleteBone b) => BindPositions[(int)b];

        // ------------------------------------------------------------------ tube generator

        /// <summary>Profile sample along a tube: s in metres from the start, half-sizes, weights.</summary>
        private struct Ring
        {
            public Vector3 Center;
            public float Rx, RzFront, RzBack;
            public int BoneA, BoneB;
            public float WeightB; // 0 => fully BoneA
        }

        /// <summary>
        /// Emits a tube through the rings. `side` gives the lateral axis (X); the front direction
        /// is cross(tangent, side). Ends are closed by shrinking radii in the profile itself.
        /// </summary>
        private void Tube(AthleteMaterial mat, List<Ring> rings, Vector3 side, float vScale = 1f)
        {
            var b = Mesh;
            int sub = (int)mat;
            int start = b.V.Count;
            float vAcc = 0;
            for (int j = 0; j < rings.Count; j++)
            {
                var r = rings[j];
                Vector3 t = j < rings.Count - 1 ? (rings[j + 1].Center - r.Center) : (r.Center - rings[j - 1].Center);
                if (j > 0 && j < rings.Count - 1) t = rings[j + 1].Center - rings[j - 1].Center;
                t = t.normalized;
                Vector3 a = (side - Vector3.Dot(side, t) * t).normalized;
                Vector3 f = Vector3.Cross(t, a);
                if (j > 0) vAcc += Vector3.Distance(r.Center, rings[j - 1].Center);
                for (int k = 0; k <= Seg; k++)
                {
                    float th = k / (float)Seg * Mathf.PI * 2f;
                    float c = Mathf.Cos(th), s = Mathf.Sin(th);
                    float rz = s >= 0 ? r.RzFront : r.RzBack;
                    Vector3 p = r.Center + a * (r.Rx * c) + f * (rz * s);
                    Vector3 n = (a * (c / Mathf.Max(1e-4f, r.Rx)) + f * (s / Mathf.Max(1e-4f, rz))).normalized;
                    int vi = b.Add(p, n, new Vector2(k / (float)Seg, vAcc * vScale));
                    b.W[vi] = Weight(r.BoneA, r.BoneB, r.WeightB);
                }
            }
            for (int j = 0; j < rings.Count - 1; j++)
                for (int k = 0; k < Seg; k++)
                {
                    int v00 = start + j * (Seg + 1) + k, v01 = v00 + 1;
                    int v10 = v00 + Seg + 1, v11 = v10 + 1;
                    b.Tri(sub, v00, v01, v10);
                    b.Tri(sub, v01, v11, v10);
                }
        }

        private static BoneWeight Weight(int a, int b, float wb)
        {
            wb = Mathf.Clamp01(wb);
            if (wb <= 0.001f || a == b) return new BoneWeight { boneIndex0 = a, weight0 = 1f };
            if (wb >= 0.999f) return new BoneWeight { boneIndex0 = b, weight0 = 1f };
            return new BoneWeight { boneIndex0 = a, weight0 = 1f - wb, boneIndex1 = b, weight1 = wb };
        }

        /// <summary>Blend weight towards bone B around a joint at `jointS` (linear over +-blend).</summary>
        private static float JointBlend(float s, float jointS, float blend) => Mathf.Clamp01((s - (jointS - blend)) / (2f * blend));

        /// <summary>Ellipsoid (closed) weighted to one bone; used for head, ears, nose, eyes.</summary>
        private void Ellipsoid(AthleteMaterial mat, Vector3 c, Vector3 r, int bone, int lon = 16, int lat = 10,
                               Func<Vector3, Vector3> deform = null, float thetaMax = Mathf.PI, Func<float, float> thetaMaxByPhi = null)
        {
            var b = Mesh;
            int sub = (int)mat;
            int start = b.V.Count;
            for (int y = 0; y <= lat; y++)
            {
                float v = y / (float)lat;
                for (int x = 0; x <= lon; x++)
                {
                    float u = x / (float)lon;
                    float ph = u * Mathf.PI * 2f;
                    float tmax = thetaMaxByPhi != null ? thetaMaxByPhi(ph) : thetaMax;
                    float th = v * tmax;
                    // Unit direction: theta from +Y, phi from +Z (front) rotating towards +X.
                    var n = new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph));
                    var local = Vector3.Scale(n, r);
                    if (deform != null) local = deform(local);
                    var nn = new Vector3(n.x / r.x, n.y / r.y, n.z / r.z).normalized;
                    int vi = b.Add(c + local, nn, new Vector2(u, 1 - v));
                    b.W[vi] = new BoneWeight { boneIndex0 = bone, weight0 = 1f };
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i0 = start + y * (lon + 1) + x, i1 = i0 + lon + 1;
                    b.Tri(sub, i0, i1, i0 + 1);
                    b.Tri(sub, i0 + 1, i1, i1 + 1);
                }
        }

        // ------------------------------------------------------------------ parts

        private void BuildLegs()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side < 0;
                int th = (int)(left ? AthleteBone.LThigh : AthleteBone.RThigh);
                int sh = (int)(left ? AthleteBone.LShin : AthleteBone.RShin);
                int ft = (int)(left ? AthleteBone.LFoot : AthleteBone.RFoot);
                int hips = (int)AthleteBone.Hips;
                Vector3 hip = BindPositions[th], knee = BindPositions[sh], ankle = BindPositions[ft];
                float thighLen = Vector3.Distance(hip, knee), shinLen = Vector3.Distance(knee, ankle);
                float w = _s * _w;

                // Skin: from inside the shorts down to the ankle (inside the sock).
                // (fraction along thigh/shin, rx, rzFront, rzBack) in metres for a 1.80 m athlete.
                var skin = new List<Ring>();
                float[,] thighProfile =
                {
                    { -0.06f, 0.088f, 0.085f, 0.095f },
                    { 0.10f, 0.085f, 0.084f, 0.09f },
                    { 0.35f, 0.077f, 0.08f, 0.078f },
                    { 0.65f, 0.064f, 0.068f, 0.062f },
                    { 0.88f, 0.053f, 0.058f, 0.05f },
                    { 1.0f, 0.05f, 0.056f, 0.048f },
                };
                for (int i = 0; i < thighProfile.GetLength(0); i++)
                {
                    float f = thighProfile[i, 0];
                    var c = Vector3.Lerp(hip, knee, f) + new Vector3(0, 0, 0.004f * _s);
                    float s = f * thighLen;
                    skin.Add(new Ring
                    {
                        Center = c, Rx = thighProfile[i, 1] * w, RzFront = thighProfile[i, 2] * w, RzBack = thighProfile[i, 3] * w,
                        BoneA = f < 0.15f ? hips : th, BoneB = th,
                        WeightB = f < 0.15f ? JointBlend(s, 0f, 0.08f * _s) : 0f
                    });
                }
                float[,] shinProfile =
                {
                    { 0.10f, 0.046f, 0.05f, 0.05f },
                    { 0.28f, 0.05f, 0.048f, 0.064f },  // calf belly at the back
                    { 0.5f, 0.044f, 0.044f, 0.05f },
                    { 0.75f, 0.034f, 0.036f, 0.036f },
                    { 0.95f, 0.03f, 0.032f, 0.033f },
                };
                for (int i = 0; i < shinProfile.GetLength(0); i++)
                {
                    float f = shinProfile[i, 0];
                    float s = f * shinLen;
                    skin.Add(new Ring
                    {
                        Center = Vector3.Lerp(knee, ankle, f),
                        Rx = shinProfile[i, 1] * w, RzFront = shinProfile[i, 2] * w, RzBack = shinProfile[i, 3] * w,
                        BoneA = th, BoneB = sh, WeightB = JointBlend(s + 0.001f, 0f, 0.06f * _s) * 0.5f + (f > 0.1f ? 0.5f : 0.25f)
                    });
                }
                // Fix weights: knee area blends thigh->shin smoothly.
                for (int i = 0; i < skin.Count; i++)
                {
                    var r = skin[i];
                    float y = r.Center.y;
                    if (r.BoneB == sh)
                    {
                        float k = Mathf.Clamp01((knee.y + 0.05f * _s - y) / (0.1f * _s));
                        r.BoneA = th; r.WeightB = k;
                    }
                    else if (r.BoneA == th && r.BoneB == th)
                    {
                        float k = Mathf.Clamp01((knee.y + 0.05f * _s - y) / (0.1f * _s));
                        r.BoneB = sh; r.WeightB = k;
                    }
                    skin[i] = r;
                }
                Tube(AthleteMaterial.Skin, skin, Vector3.right);

                // Socks with shin guards: below the knee to inside the boot.
                var sock = new List<Ring>();
                float[,] sockProfile =
                {
                    { 0.12f, 0.049f, 0.054f, 0.054f },
                    { 0.16f, 0.052f, 0.058f, 0.062f },
                    { 0.3f, 0.054f, 0.058f, 0.068f },
                    { 0.55f, 0.048f, 0.054f, 0.054f },
                    { 0.78f, 0.038f, 0.042f, 0.041f },
                    { 1.02f, 0.035f, 0.04f, 0.04f },
                };
                for (int i = 0; i < sockProfile.GetLength(0); i++)
                {
                    float f = sockProfile[i, 0];
                    sock.Add(new Ring
                    {
                        Center = Vector3.Lerp(knee, ankle, f), Rx = sockProfile[i, 1] * w, RzFront = sockProfile[i, 2] * w, RzBack = sockProfile[i, 3] * w,
                        BoneA = sh, BoneB = f > 0.95f ? ft : sh, WeightB = f > 0.95f ? 0.3f : 0f
                    });
                }
                // Turn-down cuff closes the top.
                sock.Insert(0, new Ring { Center = Vector3.Lerp(knee, ankle, 0.11f), Rx = 0.044f * w, RzFront = 0.047f * w, RzBack = 0.048f * w, BoneA = sh, BoneB = sh });
                Tube(AthleteMaterial.Socks, sock, Vector3.right);

                // Shorts leg opening (looser than the thigh) - part of the shorts submesh.
                var shortsLeg = new List<Ring>
                {
                    new Ring { Center = hip + new Vector3(side * 0.006f, 0.04f * _s, 0), Rx = 0.1f * w, RzFront = 0.1f * w, RzBack = 0.112f * w, BoneA = hips, BoneB = th, WeightB = 0.3f },
                    new Ring { Center = Vector3.Lerp(hip, knee, 0.25f) + new Vector3(side * 0.004f, 0, 0), Rx = 0.095f * w, RzFront = 0.097f * w, RzBack = 0.103f * w, BoneA = hips, BoneB = th, WeightB = 0.75f },
                    new Ring { Center = Vector3.Lerp(hip, knee, 0.42f) + new Vector3(side * 0.003f, 0, 0), Rx = 0.09f * w, RzFront = 0.093f * w, RzBack = 0.096f * w, BoneA = th, BoneB = th },
                    new Ring { Center = Vector3.Lerp(hip, knee, 0.43f), Rx = 0.075f * w, RzFront = 0.078f * w, RzBack = 0.075f * w, BoneA = th, BoneB = th },
                };
                Tube(AthleteMaterial.Shorts, shortsLeg, Vector3.right);

                BuildBoot(side, ankle, ft, sh);
            }
        }

        private void BuildBoot(int side, Vector3 ankle, int foot, int shin)
        {
            float w = _s * Mathf.Lerp(0.97f, 1.03f, Params.Build);
            // Rings along +Z from heel to toe; Rx = half width, RzFront = top height, RzBack = sole depth.
            float groundY = 0.012f * _s;
            float[,] prof =
            {
                // z,     y,     rx,     top,    sole
                { -0.075f, 0.055f, 0.012f, 0.012f, 0.012f },
                { -0.065f, 0.058f, 0.036f, 0.04f, 0.045f },
                { -0.035f, 0.063f, 0.043f, 0.052f, 0.051f },
                { 0.01f, 0.062f, 0.045f, 0.052f, 0.05f },
                { 0.06f, 0.05f, 0.048f, 0.038f, 0.038f },
                { 0.11f, 0.038f, 0.05f, 0.026f, 0.026f },
                { 0.15f, 0.031f, 0.045f, 0.02f, 0.019f },
                { 0.18f, 0.027f, 0.033f, 0.016f, 0.015f },
                { 0.195f, 0.026f, 0.012f, 0.01f, 0.009f },
            };
            var rings = new List<Ring>();
            for (int i = 0; i < prof.GetLength(0); i++)
            {
                var c = new Vector3(ankle.x + side * 0.004f * (prof[i, 0] / 0.2f), (prof[i, 1] + 0.0f) * _s, ankle.z + prof[i, 0] * _s);
                rings.Add(new Ring { Center = c, Rx = prof[i, 2] * w, RzFront = prof[i, 3] * w, RzBack = prof[i, 4] * w, BoneA = foot, BoneB = foot });
                // keep the sole on the ground
                var r = rings[i];
                float bottom = r.Center.y - r.RzBack;
                if (bottom < groundY) { r.Center.y += groundY - bottom; rings[i] = r; }
            }
            // Side axis X, tangent +Z -> front = cross(Z, X) = +Y: RzFront is the top of the boot.
            Tube(AthleteMaterial.Boots, rings, Vector3.right);
            // Ankle collar connecting boot and sock.
            var collar = new List<Ring>
            {
                new Ring { Center = ankle + new Vector3(0, 0.05f * _s, -0.01f * _s), Rx = 0.037f * w, RzFront = 0.042f * w, RzBack = 0.042f * w, BoneA = shin, BoneB = foot, WeightB = 0.5f },
                new Ring { Center = ankle + new Vector3(0, 0.0f, -0.008f * _s), Rx = 0.042f * w, RzFront = 0.05f * w, RzBack = 0.05f * w, BoneA = foot, BoneB = foot },
                new Ring { Center = ankle + new Vector3(0, -0.03f * _s, -0.005f * _s), Rx = 0.04f * w, RzFront = 0.048f * w, RzBack = 0.048f * w, BoneA = foot, BoneB = foot },
            };
            Tube(AthleteMaterial.Boots, collar, Vector3.right);
        }

        private void BuildTorso()
        {
            int hips = (int)AthleteBone.Hips, spine = (int)AthleteBone.Spine, chest = (int)AthleteBone.Chest, neck = (int)AthleteBone.Neck;
            float w = _w;
            // Shorts: pelvis from crotch to waistband. (y, rx, rzFront, rzBack)
            float[,] shorts =
            {
                { 0.80f, 0.02f, 0.02f, 0.02f },
                { 0.81f, 0.09f, 0.07f, 0.08f },
                { 0.84f, 0.155f, 0.095f, 0.115f },
                { 0.90f, 0.175f, 0.105f, 0.125f },
                { 0.96f, 0.172f, 0.105f, 0.115f },
                { 1.02f, 0.162f, 0.1f, 0.1f },
                { 1.035f, 0.16f, 0.099f, 0.098f },
            };
            var sRings = new List<Ring>();
            for (int i = 0; i < shorts.GetLength(0); i++)
            {
                float y = shorts[i, 0];
                sRings.Add(new Ring { Center = S(0, y, 0.005f) / 1f, Rx = shorts[i, 1] * _s * w, RzFront = shorts[i, 2] * _s * w, RzBack = shorts[i, 3] * _s * w, BoneA = hips, BoneB = hips });
            }
            Tube(AthleteMaterial.Shorts, sRings, Vector3.right);

            // Shirt from the hem (over the shorts) to the collar.
            float[,] shirt =
            {
                { 0.965f, 0.172f, 0.108f, 0.11f },
                { 1.0f, 0.168f, 0.106f, 0.107f },
                { 1.06f, 0.158f, 0.103f, 0.104f },
                { 1.15f, 0.163f, 0.109f, 0.104f },
                { 1.24f, 0.174f, 0.119f, 0.105f },
                { 1.32f, 0.184f, 0.124f, 0.108f },
                { 1.39f, 0.192f, 0.115f, 0.108f },
                { 1.435f, 0.18f, 0.098f, 0.098f },
                { 1.465f, 0.14f, 0.082f, 0.082f },
                { 1.49f, 0.085f, 0.068f, 0.07f },
                { 1.505f, 0.072f, 0.07f, 0.072f },
                { 1.5f, 0.066f, 0.064f, 0.066f },
            };
            var tRings = new List<Ring>();
            for (int i = 0; i < shirt.GetLength(0); i++)
            {
                float y = shirt[i, 0];
                int a, bb; float wb;
                if (y < 1.12f) { a = hips; bb = spine; wb = Mathf.InverseLerp(0.98f, 1.12f, y); }
                else if (y < 1.3f) { a = spine; bb = chest; wb = Mathf.InverseLerp(1.14f, 1.3f, y); }
                else { a = chest; bb = neck; wb = Mathf.InverseLerp(1.47f, 1.52f, y) * 0.5f; }
                float zOff = y > 1.4f ? -0.012f : 0.005f;
                tRings.Add(new Ring { Center = S(0, y, zOff) , Rx = shirt[i, 1] * _s * w, RzFront = shirt[i, 2] * _s * w, RzBack = shirt[i, 3] * _s * w, BoneA = a, BoneB = bb, WeightB = wb });
            }
            // Keep x unscaled by S() for centre (S scales x by width but centre x is 0 anyway).
            Tube(AthleteMaterial.Shirt, tRings, Vector3.right);

            // Neck (skin), from inside the collar to under the jaw.
            var nRings = new List<Ring>
            {
                new Ring { Center = S(0, 1.46f, -0.02f), Rx = 0.071f * _s * w, RzFront = 0.069f * _s, RzBack = 0.073f * _s, BoneA = chest, BoneB = neck, WeightB = 0.4f },
                new Ring { Center = S(0, 1.53f, -0.015f), Rx = 0.063f * _s * w, RzFront = 0.06f * _s, RzBack = 0.065f * _s, BoneA = neck, BoneB = neck },
                new Ring { Center = S(0, 1.6f, -0.012f), Rx = 0.058f * _s * w, RzFront = 0.055f * _s, RzBack = 0.062f * _s, BoneA = neck, BoneB = (int)AthleteBone.Head, WeightB = 0.6f },
                new Ring { Center = S(0, 1.63f, -0.015f), Rx = 0.04f * _s * w, RzFront = 0.035f * _s, RzBack = 0.045f * _s, BoneA = (int)AthleteBone.Head, BoneB = (int)AthleteBone.Head },
            };
            Tube(AthleteMaterial.Skin, nRings, Vector3.right);

            // Collar trim on the shirt (thin ring, rendered with the shorts colour as accent).
            var collar = new List<Ring>
            {
                new Ring { Center = S(0, 1.49f, -0.015f), Rx = 0.074f * _s * w, RzFront = 0.072f * _s, RzBack = 0.076f * _s, BoneA = chest, BoneB = neck, WeightB = 0.3f },
                new Ring { Center = S(0, 1.515f, -0.018f), Rx = 0.071f * _s * w, RzFront = 0.069f * _s, RzBack = 0.073f * _s, BoneA = chest, BoneB = neck, WeightB = 0.4f },
            };
            Tube(AthleteMaterial.Shorts, collar, Vector3.right);
        }

        private void BuildArms()
        {
            int chest = (int)AthleteBone.Chest;
            for (int side = -1; side <= 1; side += 2)
            {
                bool left = side < 0;
                int clav = (int)(left ? AthleteBone.LClavicle : AthleteBone.RClavicle);
                int ua = (int)(left ? AthleteBone.LUpperArm : AthleteBone.RUpperArm);
                int fa = (int)(left ? AthleteBone.LForeArm : AthleteBone.RForeArm);
                int hand = (int)(left ? AthleteBone.LHand : AthleteBone.RHand);
                Vector3 sh = BindPositions[ua], el = BindPositions[fa], wr = BindPositions[hand];
                Vector3 dir = ArmDir(side);
                float w = _s * _w;
                float upper = Vector3.Distance(sh, el), lower = Vector3.Distance(el, wr);
                // Lateral axis for arm tubes: the body's forward cross arm direction keeps front/back consistent.
                // Lateral axis must point to the body's right (+X) so that cross(dir, lateral) = +Z (front).
                Vector3 lateral = Vector3.Cross(Vector3.forward, dir);
                if (lateral.x < 0) lateral = -lateral;

                var skin = new List<Ring>();
                // (distance from shoulder in metres along arm, rx, rzFront, rzBack, boneA, boneB, wB)
                void R(float d, float rx, float rf, float rb)
                {
                    Vector3 c = sh + dir * (d * _s);
                    int a; int b; float wb;
                    float elbowD = upper / _s;
                    if (d < 0.06f) { a = clav; b = ua; wb = Mathf.InverseLerp(-0.04f, 0.06f, d); }
                    else if (d < elbowD - 0.04f) { a = ua; b = ua; wb = 0; }
                    else if (d < elbowD + 0.04f) { a = ua; b = fa; wb = Mathf.InverseLerp(elbowD - 0.04f, elbowD + 0.04f, d); }
                    else if (d < elbowD + lower / _s - 0.03f) { a = fa; b = fa; wb = 0; }
                    else { a = fa; b = hand; wb = Mathf.InverseLerp(elbowD + lower / _s - 0.03f, elbowD + lower / _s + 0.02f, d); }
                    skin.Add(new Ring { Center = c, Rx = rx * w, RzFront = rf * w, RzBack = rb * w, BoneA = a, BoneB = b, WeightB = wb });
                }
                R(-0.045f, 0.035f, 0.035f, 0.035f);
                R(-0.02f, 0.06f, 0.064f, 0.064f);
                R(0.04f, 0.066f, 0.068f, 0.064f);  // deltoid
                R(0.12f, 0.058f, 0.062f, 0.062f);  // biceps/triceps
                R(0.2f, 0.051f, 0.055f, 0.054f);
                R(0.28f, 0.043f, 0.046f, 0.044f);  // elbow
                R(0.33f, 0.048f, 0.05f, 0.047f);   // forearm muscle
                R(0.42f, 0.04f, 0.04f, 0.038f);
                R(0.52f, 0.031f, 0.026f, 0.026f);  // wrist
                R(0.555f, 0.032f, 0.024f, 0.024f);
                Tube(AthleteMaterial.Skin, skin, lateral);

                // Hand: flattened paddle with thumb bulge, fingers slightly curled (closed end).
                var hRings = new List<Ring>();
                float[,] hp = { { 0.0f, 0.03f, 0.02f, 0.02f }, { 0.04f, 0.042f, 0.022f, 0.02f }, { 0.08f, 0.044f, 0.02f, 0.018f }, { 0.12f, 0.04f, 0.016f, 0.015f }, { 0.155f, 0.03f, 0.013f, 0.012f }, { 0.17f, 0.012f, 0.008f, 0.008f } };
                for (int i = 0; i < hp.GetLength(0); i++)
                {
                    float d = hp[i, 0] * _s;
                    // Curl the fingers towards the body (palm facing in).
                    Vector3 c = wr + dir * d + new Vector3(-side * 0.0f, 0, 0.012f * (d / (0.17f * _s)) * _s);
                    hRings.Add(new Ring { Center = c, Rx = hp[i, 1] * _s, RzFront = hp[i, 2] * _s, RzBack = hp[i, 3] * _s, BoneA = hand, BoneB = hand });
                }
                // Palm faces the thigh: hand lateral axis is front-back.
                if (!Params.Goalkeeper) Tube(AthleteMaterial.Skin, hRings, Vector3.forward);
                else Tube(AthleteMaterial.Boots, hRings.ConvertAll(r => { r.Rx *= 1.25f; r.RzFront *= 1.4f; r.RzBack *= 1.4f; return r; }), Vector3.forward);

                // Sleeve.
                float sleeveEnd = Params.LongSleeves ? 0.53f : 0.17f;
                var sleeve = new List<Ring>();
                void SR(float d, float rx, float rf, float rb, int a, int b, float wb)
                {
                    sleeve.Add(new Ring { Center = sh + dir * (d * _s), Rx = rx * w, RzFront = rf * w, RzBack = rb * w, BoneA = a, BoneB = b, WeightB = wb });
                }
                SR(-0.05f, 0.045f, 0.05f, 0.05f, chest, clav, 0.5f);
                SR(-0.025f, 0.068f, 0.071f, 0.071f, clav, ua, 0.3f);
                SR(0.04f, 0.071f, 0.073f, 0.069f, clav, ua, 0.85f);
                if (Params.LongSleeves)
                {
                    SR(0.15f, 0.064f, 0.068f, 0.068f, ua, ua, 0f);
                    SR(0.28f, 0.051f, 0.054f, 0.052f, ua, fa, 0.5f);
                    SR(0.42f, 0.046f, 0.046f, 0.044f, fa, fa, 0f);
                    SR(0.52f, 0.037f, 0.033f, 0.033f, fa, hand, 0.2f);
                    SR(0.525f, 0.03f, 0.027f, 0.027f, fa, hand, 0.2f);
                }
                else
                {
                    SR(sleeveEnd, 0.065f, 0.068f, 0.067f, ua, ua, 0f);
                    SR(sleeveEnd + 0.003f, 0.057f, 0.061f, 0.061f, ua, ua, 0f);
                }
                Tube(AthleteMaterial.Shirt, sleeve, lateral);
            }
        }

        private void BuildHead()
        {
            int head = (int)AthleteBone.Head;
            var p = Params;
            float fw = Mathf.Lerp(0.94f, 1.06f, p.FaceWidth);
            float fl = Mathf.Lerp(0.96f, 1.04f, p.FaceLength);
            Vector3 c = S(0, 1.69f, 0.005f);
            c.x = 0;
            // Real head: ~0.155 m wide, ~0.2 m deep, ~0.23 m chin to crown.
            Vector3 r = new Vector3(0.077f * fw, 0.112f * fl, 0.098f) * _s;
            // Skull/face shape: narrower jaw and chin, flatter face front, fuller back of skull.
            Vector3 Deform(Vector3 v)
            {
                float ny = v.y / r.y;
                float nz = v.z / r.z;
                float x = v.x, y = v.y, z = v.z;
                if (ny < 0f)
                {
                    float jaw = Mathf.Lerp(1f, 0.62f * fw, Mathf.Pow(-ny, 1.6f));
                    x *= jaw;
                    // Chin forward, throat back.
                    if (nz > 0) z *= Mathf.Lerp(1f, 0.92f, -ny); else z *= Mathf.Lerp(1f, 0.55f, -ny);
                }
                else
                {
                    // Cranium slightly wider at the back-top.
                    if (nz < 0) z *= 1f + 0.06f * ny;
                }
                if (nz > 0.6f) z -= (nz - 0.6f) * 0.012f * _s; // flatten the face plane
                return new Vector3(x, y, z);
            }
            Ellipsoid(AthleteMaterial.Skin, c, r, head, 24, 16, Deform);

            // Facial features.
            Ellipsoid(AthleteMaterial.Skin, c + new Vector3(0, -0.012f, 0.094f) * _s, new Vector3(0.012f, 0.026f, 0.018f) * _s, head, 8, 6);   // nose
            Ellipsoid(AthleteMaterial.Skin, c + new Vector3(0, -0.06f, 0.083f) * _s, new Vector3(0.022f, 0.008f, 0.01f) * _s, head, 8, 5);    // lips
            for (int side = -1; side <= 1; side += 2)
            {
                Ellipsoid(AthleteMaterial.Skin, c + new Vector3(side * 0.077f * fw, -0.005f, -0.005f) * _s, new Vector3(0.011f, 0.03f, 0.019f) * _s, head, 8, 6); // ears
                Ellipsoid(AthleteMaterial.Eyes, c + new Vector3(side * 0.031f * fw, 0.012f, 0.08f) * _s, new Vector3(0.0115f, 0.007f, 0.008f) * _s, head, 8, 5); // eyes
                Ellipsoid(AthleteMaterial.Hair, c + new Vector3(side * 0.032f * fw, 0.03f, 0.086f) * _s, new Vector3(0.022f, 0.0045f, 0.007f) * _s, head, 8, 4); // brows
                Ellipsoid(AthleteMaterial.Skin, c + new Vector3(side * 0.042f * fw, -0.022f, 0.07f) * _s, new Vector3(0.024f, 0.018f, 0.018f) * _s, head, 8, 6); // cheekbones
            }

            BuildHair(c, r, head);
            BuildBeard(c, r, head, fw);
        }

        private void BuildHair(Vector3 c, Vector3 r, int head)
        {
            float t;
            Vector3 hc = c + new Vector3(0, 0.004f, -0.004f) * _s;
            // Hairline: theta limit by azimuth (phi = 0 at the front).
            Func<float, float> Line(float front, float side, float back) => ph =>
            {
                float cf = Mathf.Cos(ph); // 1 front, -1 back
                float k = cf >= 0 ? Mathf.Lerp(side, front, cf) : Mathf.Lerp(side, back, -cf);
                return k * Mathf.Deg2Rad;
            };
            switch (Params.Hair)
            {
                case HairStyle.Bald:
                    return;
                case HairStyle.Buzz:
                    t = 0.003f;
                    Ellipsoid(AthleteMaterial.Hair, hc, r + Vector3.one * t * _s, head, 20, 8, null, 0, Line(58, 92, 112));
                    break;
                case HairStyle.Short:
                    t = 0.01f;
                    Ellipsoid(AthleteMaterial.Hair, hc + new Vector3(0, 0.004f, 0) * _s, r + new Vector3(t, t * 1.3f, t) * _s, head, 20, 8, null, 0, Line(56, 90, 110));
                    break;
                case HairStyle.Crop:
                    t = 0.014f;
                    Ellipsoid(AthleteMaterial.Hair, hc + new Vector3(0, 0.01f, 0.004f) * _s, r + new Vector3(t * 0.8f, t * 1.6f, t) * _s, head, 20, 8, null, 0, Line(50, 88, 108));
                    break;
                case HairStyle.Medium:
                    t = 0.022f;
                    Ellipsoid(AthleteMaterial.Hair, hc + new Vector3(0, 0.006f, -0.006f) * _s, r + new Vector3(t, t * 1.2f, t * 1.2f) * _s, head, 20, 10, null, 0, Line(55, 98, 128));
                    break;
                case HairStyle.Curly:
                    t = 0.03f;
                    Ellipsoid(AthleteMaterial.Hair, hc + new Vector3(0, 0.012f, -0.004f) * _s, r + new Vector3(t, t * 1.1f, t) * _s, head, 20, 9,
                        v => v * (1f + 0.06f * Mathf.Sin(v.x * 180f) * Mathf.Sin(v.z * 170f)), 0, Line(52, 92, 112));
                    break;
                case HairStyle.Afro:
                    t = 0.045f;
                    Ellipsoid(AthleteMaterial.Hair, hc + new Vector3(0, 0.022f, -0.01f) * _s, r + new Vector3(t, t * 0.85f, t) * _s, head, 22, 12,
                        v => v * (1f + 0.035f * Mathf.Sin(v.x * 150f) * Mathf.Sin(v.y * 160f)), 0, Line(62, 102, 122));
                    break;
                case HairStyle.Bun:
                    t = 0.008f;
                    Ellipsoid(AthleteMaterial.Hair, hc, r + Vector3.one * t * _s, head, 20, 8, null, 0, Line(56, 92, 115));
                    Ellipsoid(AthleteMaterial.Hair, hc + new Vector3(0, 0.085f, -0.06f) * _s, new Vector3(0.032f, 0.03f, 0.032f) * _s, head, 10, 8);
                    break;
            }
        }

        private void BuildBeard(Vector3 c, Vector3 r, int head, float fw)
        {
            if (Params.Beard == BeardStyle.None) return;
            float t = Params.Beard == BeardStyle.Stubble ? 0.0015f : Params.Beard == BeardStyle.Short ? 0.006f : Params.Beard == BeardStyle.Full ? 0.013f : 0.008f;
            // Lower face shell: theta from ~95 deg (cheek line) to the bottom, only towards the front.
            var rr = r + Vector3.one * t * _s;
            int lon = 16, lat = 6;
            var b = Mesh;
            int start = b.V.Count;
            bool goatee = Params.Beard == BeardStyle.Goatee;
            float phMax = goatee ? 0.45f : 1.75f;
            for (int y = 0; y <= lat; y++)
            {
                float th = Mathf.Lerp(goatee ? 118f : 98f, 165f, y / (float)lat) * Mathf.Deg2Rad;
                for (int x = 0; x <= lon; x++)
                {
                    float ph = Mathf.Lerp(-phMax, phMax, x / (float)lon);
                    var n = new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Cos(ph));
                    var v = Vector3.Scale(n, rr);
                    float ny = v.y / rr.y;
                    if (ny < 0) { v.x *= Mathf.Lerp(1f, 0.62f * fw, Mathf.Pow(-ny, 1.6f)); if (v.z > 0) v.z *= Mathf.Lerp(1f, 0.92f, -ny); else v.z *= Mathf.Lerp(1f, 0.55f, -ny); }
                    int vi = b.Add(c + v, n, new Vector2(x / (float)lon, y / (float)lat));
                    b.W[vi] = new BoneWeight { boneIndex0 = head, weight0 = 1 };
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i0 = start + y * (lon + 1) + x, i1 = i0 + lon + 1;
                    b.Tri((int)AthleteMaterial.Hair, i0, i1, i0 + 1);
                    b.Tri((int)AthleteMaterial.Hair, i0 + 1, i1, i1 + 1);
                }
        }
    }
}
