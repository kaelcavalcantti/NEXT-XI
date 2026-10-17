using System.Collections.Generic;
using NextXI.Core.Match;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Options for the procedural stadium.</summary>
    public sealed class StadiumOptions
    {
        public PitchSpec Pitch = new PitchSpec();
        public Color HomeColor = new Color(0.78f, 0.06f, 0.18f);
        public Color AwayColor = new Color(0.12f, 0.23f, 0.54f);
        public Color SeatColor = new Color(0.15f, 0.17f, 0.22f);
        /// <summary>0 = empty, 3 = full.</summary>
        public int CrowdDensity = 2;
        public bool Night = true;
        public string StadiumName = "ARENA NEXT";
        public int Seed = 7;
    }

    /// <summary>
    /// Builds the whole match environment procedurally: turf, markings, goals with nets,
    /// corner flags, LED boards, two-tier stands with crowd, roof, floodlight towers and lights.
    /// </summary>
    public sealed class StadiumBuilder
    {
        public Transform Root { get; private set; }
        public readonly List<Renderer> CrowdGroups = new List<Renderer>();
        public readonly List<Material> CrowdMaterials = new List<Material>();
        public Light Sun { get; private set; }
        private StadiumOptions _o;

        public Transform Build(StadiumOptions o, Transform parent = null)
        {
            _o = o;
            Root = new GameObject("Stadium").transform;
            if (parent != null) Root.SetParent(parent, false);
            BuildTurf();
            BuildMarkings();
            BuildGoal(-1);
            BuildGoal(+1);
            BuildCornerFlags();
            BuildBoards();
            BuildStands();
            BuildFloodlights();
            BuildLighting();
            return Root;
        }

        private PitchSpec P => _o.Pitch;

        private void BuildTurf()
        {
            float stripe = P.Length / 18f;
            float tile = stripe * 2f;
            float hx = P.HalfLength + 9f, hz = P.HalfWidth + 7f;
            var b = new MeshBuilder();
            int n = 24;
            // Subdivided plane so lighting/fog interpolate nicely.
            for (int iz = 0; iz <= n; iz++)
                for (int ix = 0; ix <= n; ix++)
                {
                    float x = Mathf.Lerp(-hx, hx, ix / (float)n), z = Mathf.Lerp(-hz, hz, iz / (float)n);
                    // Align stripes with the pitch: stripe boundaries at multiples of `stripe` from the goal line.
                    float u = (x + P.HalfLength) / tile, v = (z + P.HalfWidth) / tile;
                    b.Add(new Vector3(x, 0, z), Vector3.up, new Vector2(u, v));
                }
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    int i0 = iz * (n + 1) + ix, i1 = i0 + 1, i2 = i0 + n + 1, i3 = i2 + 1;
                    b.Tri(0, i0, i2, i1);
                    b.Tri(0, i1, i2, i3);
                }
            var mat = Mat.Opaque("turf", Color.white, 0.12f, 0f, ProcTex.Grass());
            var go = MeshKitUnity.Spawn("Turf", b.Build("Turf"), new[] { mat }, Root, false, true);
            go.isStatic = true;

            // Track/surround beyond the turf.
            var s = new MeshBuilder();
            MeshKit.Box(s, 0, new Vector3(0, -0.06f, 0), new Vector3(hx * 2 + 30, 0.1f, hz * 2 + 30), Quaternion.identity);
            MeshKitUnity.Spawn("Surround", s.Build("Surround"), new[] { Mat.Opaque("surround", new Color(0.2f, 0.33f, 0.2f), 0.05f) }, Root, false, true).isStatic = true;
        }

        private void BuildMarkings()
        {
            const float w = 0.12f, y = 0.006f;
            var b = new MeshBuilder();
            float L = P.HalfLength, W = P.HalfWidth;
            void Line(float x0, float z0, float x1, float z1) => MeshKit.FlatQuad(b, 0, new Vector3(x0, 0, z0), new Vector3(x1, 0, z1), w, y);
            Line(-L - w / 2, W, L + w / 2, W);
            Line(-L - w / 2, -W, L + w / 2, -W);
            Line(-L, -W, -L, W);
            Line(L, -W, L, W);
            Line(0, -W, 0, W);
            MeshKit.FlatArc(b, 0, Vector3.zero, P.CentreCircleRadius, 0, Mathf.PI * 2, w, y, 72);
            MeshKit.FlatArc(b, 0, Vector3.zero, 0.15f, 0, Mathf.PI * 2, 0.3f, y, 12);
            for (int side = -1; side <= 1; side += 2)
            {
                float gl = side * L;
                float pa = side * (L - P.PenaltyAreaDepth), ga = side * (L - P.GoalAreaDepth);
                float pw = P.PenaltyAreaWidth / 2, gw = P.GoalAreaWidth / 2;
                Line(gl, -pw, pa, -pw); Line(gl, pw, pa, pw); Line(pa, -pw, pa, pw);
                Line(gl, -gw, ga, -gw); Line(gl, gw, ga, gw); Line(ga, -gw, ga, gw);
                var spot = new Vector3(side * (L - P.PenaltySpotDistance), 0, 0);
                MeshKit.FlatArc(b, 0, spot, 0.11f, 0, Mathf.PI * 2, 0.22f, y, 12);
                // Penalty arc: part of the 9.15 m circle outside the area.
                float d = P.PenaltyAreaDepth - P.PenaltySpotDistance;
                float half = Mathf.Acos(d / P.CentreCircleRadius);
                float baseAng = side > 0 ? Mathf.PI : 0f;
                MeshKit.FlatArc(b, 0, spot, P.CentreCircleRadius, baseAng - half, baseAng + half, w, y, 32);
                // Corner arcs.
                for (int zs = -1; zs <= 1; zs += 2)
                {
                    var c = new Vector3(side * L, 0, zs * W);
                    float a0 = side > 0 ? (zs > 0 ? Mathf.PI : Mathf.PI * 0.5f) : (zs > 0 ? -Mathf.PI * 0.5f : 0f);
                    MeshKit.FlatArc(b, 0, c, P.CornerArcRadius, a0, a0 + Mathf.PI * 0.5f, w, y, 10);
                }
            }
            var mat = Mat.Opaque("lines", new Color(0.93f, 0.95f, 0.93f), 0.2f);
            MeshKitUnity.Spawn("Markings", b.Build("Markings"), new[] { mat }, Root, false, true).isStatic = true;
        }

        private void BuildGoal(int side)
        {
            var root = new GameObject(side < 0 ? "GoalWest" : "GoalEast").transform;
            root.SetParent(Root, false);
            float x = side * P.HalfLength, hw = P.HalfGoalWidth, gh = P.GoalHeight, d = P.GoalDepth, r = P.PostRadius;
            var frame = new MeshBuilder();
            // Posts are placed so that their back edge is on the goal line (Laws of the Game).
            float px = x + side * r;
            MeshKit.Cylinder(frame, 0, new Vector3(px, 0, -hw - r), new Vector3(px, gh + r, -hw - r), r, 14);
            MeshKit.Cylinder(frame, 0, new Vector3(px, 0, hw + r), new Vector3(px, gh + r, hw + r), r, 14);
            MeshKit.Cylinder(frame, 0, new Vector3(px, gh + r, -hw - 2 * r), new Vector3(px, gh + r, hw + 2 * r), r, 14);
            // Back stanchions supporting the net.
            float bx = x + side * d;
            MeshKit.Cylinder(frame, 0, new Vector3(bx, 0, -hw), new Vector3(bx, gh * 0.85f, -hw), 0.025f, 8);
            MeshKit.Cylinder(frame, 0, new Vector3(bx, 0, hw), new Vector3(bx, gh * 0.85f, hw), 0.025f, 8);
            MeshKit.Cylinder(frame, 0, new Vector3(bx, gh * 0.85f, -hw), new Vector3(bx, gh * 0.85f, hw), 0.025f, 8);
            MeshKitUnity.Spawn("Frame", frame.Build("GoalFrame"), new[] { Mat.Opaque("goalframe", Color.white, 0.75f) }, root);

            var net = new MeshBuilder();
            var uv = new Vector2(1.6f, 1.6f);
            MeshKit.Wall(net, 0, new Vector3(bx, 0, -hw), new Vector3(bx, 0, hw), 0, gh * 0.85f, uv, true);
            MeshKit.Wall(net, 0, new Vector3(x, 0, -hw), new Vector3(bx, 0, -hw), 0, gh, uv, true);
            MeshKit.Wall(net, 0, new Vector3(x, 0, hw), new Vector3(bx, 0, hw), 0, gh, uv, true);
            // Sloped roof net from crossbar to the back top bar.
            int i0 = net.Add(new Vector3(x, gh, -hw), Vector3.up, new Vector2(0, 0));
            int i1 = net.Add(new Vector3(x, gh, hw), Vector3.up, new Vector2(hw * 2 * uv.x, 0));
            int i2 = net.Add(new Vector3(bx, gh * 0.85f, hw), Vector3.up, new Vector2(hw * 2 * uv.x, d * uv.y));
            int i3 = net.Add(new Vector3(bx, gh * 0.85f, -hw), Vector3.up, new Vector2(0, d * uv.y));
            net.Tri(0, i0, i1, i2); net.Tri(0, i0, i2, i3);
            net.Tri(0, i0, i2, i1); net.Tri(0, i0, i3, i2);
            var netMat = Mat.Cutout("net", ProcTex.Net(), new Color(0.95f, 0.95f, 0.95f));
            MeshKitUnity.Spawn("Net", net.Build("GoalNet", true), new[] { netMat }, root, false, false);
        }

        private void BuildCornerFlags()
        {
            var b = new MeshBuilder(2);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var basePos = new Vector3(sx * P.HalfLength, 0, sz * P.HalfWidth);
                    MeshKit.Cylinder(b, 0, basePos, basePos + Vector3.up * 1.5f, 0.018f, 8);
                    MeshKit.Box(b, 1, basePos + new Vector3(0, 1.38f, -sz * 0.2f), new Vector3(0.01f, 0.28f, 0.38f), Quaternion.identity);
                }
            MeshKitUnity.Spawn("CornerFlags", b.Build("Flags"), new[] { Mat.Opaque("flagpole", Color.white, 0.5f), Mat.Opaque("flag", new Color(1f, 0.45f, 0.08f), 0.2f) }, Root);
        }

        private void BuildBoards()
        {
            string[] msgs = { "NEXT XI", "VOLTA ENERGY", "KORU AIR", "NOVA BANK", "PULSE MOBILE", "ARENA NEXT" };
            var tex = ProcTex.LedBoard(msgs, new Color(0.04f, 0.06f, 0.14f), new Color(0.95f, 0.97f, 1f), new Color(1f, 0.47f, 0.12f));
            var mat = Mat.UnlitTex("led", tex, Color.white);
            var b = new MeshBuilder(2);
            float L = P.HalfLength + 4.5f, W = P.HalfWidth + 4f, h = 0.9f;
            var uvs = new Vector2(1f / 40f, 1f / h);
            // Board faces (towards the pitch) + back/frame.
            void Board(Vector3 a, Vector3 c)
            {
                MeshKit.Wall(b, 0, a, c, 0.05f, h, uvs, false);
                Vector3 dir = (c - a).normalized;
                Vector3 n = Vector3.Cross(dir, Vector3.up);
                MeshKit.Box(b, 1, (a + c) * 0.5f - n * 0.08f + Vector3.up * h * 0.5f, new Vector3((c - a).magnitude, h + 0.1f, 0.12f), Quaternion.LookRotation(n));
            }
            Board(new Vector3(L - 2, 0, W), new Vector3(-L + 2, 0, W));
            Board(new Vector3(-L + 2, 0, -W), new Vector3(L - 2, 0, -W));
            Board(new Vector3(L, 0, -P.HalfGoalWidth - 6f), new Vector3(L, 0, -W + 2));
            Board(new Vector3(L, 0, W - 2), new Vector3(L, 0, P.HalfGoalWidth + 6f));
            Board(new Vector3(-L, 0, -W + 2), new Vector3(-L, 0, -P.HalfGoalWidth - 6f));
            Board(new Vector3(-L, 0, P.HalfGoalWidth + 6f), new Vector3(-L, 0, W - 2));
            MeshKitUnity.Spawn("LedBoards", b.Build("LedBoards"), new[] { mat, Mat.Opaque("boardframe", new Color(0.08f, 0.08f, 0.1f), 0.4f) }, Root, false, false);
        }

        private void BuildStands()
        {
            var stands = new GameObject("Stands").transform;
            stands.SetParent(Root, false);
            var concrete = Mat.Opaque("concrete", new Color(0.36f, 0.37f, 0.4f), 0.1f);
            var seats = Mat.Opaque("seats", Color.white, 0.25f, 0f, ProcTex.Seats(_o.SeatColor));
            var crowdTex = ProcTex.Crowd(_o.HomeColor, _o.AwayColor, _o.Seed);
            CrowdMaterials.Clear();
            for (int g = 0; g < 3; g++)
            {
                var cm = Mat.Cutout("crowd" + g, crowdTex, Color.white, false);
                cm.SetTextureScale(cm.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", new Vector2(1f, 0.5f));
                CrowdMaterials.Add(cm);
            }
            // Four stands: (centre, along-direction, outward normal, length)
            float L = P.HalfLength, W = P.HalfWidth;
            var defs = new[]
            {
                (new Vector3(0, 0, W + 9f), Vector3.right, Vector3.forward, P.Length + 10f),
                (new Vector3(0, 0, -W - 9f), Vector3.left, Vector3.back, P.Length + 10f),
                (new Vector3(L + 10f, 0, 0), Vector3.back, Vector3.right, P.Width + 8f),
                (new Vector3(-L - 10f, 0, 0), Vector3.forward, Vector3.left, P.Width + 8f),
            };
            int idx = 0;
            foreach (var (c, along, outward, len) in defs)
            {
                BuildStand($"Stand{idx++}", stands, c, along, outward, len, concrete, seats);
            }
            // Corner fillers (dark structure).
            var fill = new MeshBuilder();
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    MeshKit.Box(fill, 0, new Vector3(sx * (L + 14f), 9f, sz * (W + 13f)), new Vector3(12f, 18f, 12f), Quaternion.Euler(0, 45f * sx * sz, 0));
            MeshKitUnity.Spawn("Corners", fill.Build("Corners"), new[] { concrete }, stands, false, false);
        }

        private void BuildStand(string name, Transform parent, Vector3 front, Vector3 along, Vector3 outward, float length, Material concrete, Material seats)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var structure = new MeshBuilder(2);
            var crowd = new[] { new MeshBuilder(), new MeshBuilder(), new MeshBuilder() };
            const int rowsLower = 14, rowsUpper = 12;
            const float rowDepth = 0.85f, rowRise = 0.45f;
            int density = _o.CrowdDensity;
            var rng = new System.Random(name.GetHashCode());
            float tierGap = 2.4f;
            int row = 0;
            for (int tier = 0; tier < 2; tier++)
            {
                int rows = tier == 0 ? rowsLower : rowsUpper;
                float baseH = tier == 0 ? 1.2f : 1.2f + rowsLower * rowRise + tierGap + 2f;
                float baseD = tier == 0 ? 0f : rowsLower * rowDepth - 3f;
                for (int r = 0; r < rows; r++, row++)
                {
                    float d = baseD + r * rowDepth;
                    float h = baseH + r * rowRise;
                    Vector3 c = front + outward * (d + rowDepth * 0.5f) + Vector3.up * (h - rowRise * 0.5f);
                    MeshKit.Box(structure, 1, c, new Vector3(length, rowRise, rowDepth), Quaternion.LookRotation(outward), new Vector2(1f / 0.5f, 1f / 0.5f));
                    MeshKit.Box(structure, 0, c - Vector3.up * (h * 0.5f), new Vector3(length, h - rowRise, rowDepth), Quaternion.LookRotation(outward));
                    // Spectators: a card strip facing the pitch on top of the step.
                    float fill = density == 0 ? 0f : density == 1 ? 0.45f : density == 2 ? 0.75f : 0.95f;
                    if (rng.NextDouble() < fill)
                    {
                        Vector3 a = front + outward * (d + rowDepth * 0.45f) + Vector3.up * h - along * length * 0.5f;
                        Vector3 b = a + along * length;
                        float hh = 1.1f;
                        var cb = crowd[row % 3];
                        Vector3 n = -outward;
                        float uLen = length / 6f; // ~6 m per texture repeat (24 people)
                        float u0 = (float)rng.NextDouble();
                        int i0 = cb.Add(b, n, new Vector2(u0, 0));
                        int i1 = cb.Add(a, n, new Vector2(u0 + uLen, 0));
                        int i2 = cb.Add(a + Vector3.up * hh - outward * 0.1f, n, new Vector2(u0 + uLen, 1));
                        int i3 = cb.Add(b + Vector3.up * hh - outward * 0.1f, n, new Vector2(u0, 1));
                        cb.Tri(0, i0, i1, i2); cb.Tri(0, i0, i2, i3);
                    }
                }
                // Tier fascia (front wall), shows the stadium name on the upper tier.
                Vector3 fa = front + outward * baseD + Vector3.up * baseH;
                MeshKit.Box(structure, 0, fa + Vector3.up * -0.6f - outward * 0.1f, new Vector3(length, 1.2f, 0.2f), Quaternion.LookRotation(outward));
            }
            // Back wall and roof.
            float totalD = rowsLower * rowDepth - 3f + rowsUpper * rowDepth + 1f;
            float topH = 1.2f + rowsLower * rowRise + tierGap + 2f + rowsUpper * rowRise;
            MeshKit.Box(structure, 0, front + outward * totalD + Vector3.up * (topH * 0.5f + 2f), new Vector3(length, topH + 4f, 0.6f), Quaternion.LookRotation(outward));
            var roof = new MeshBuilder();
            MeshKit.Box(roof, 0, front + outward * (totalD * 0.5f + 1f) + Vector3.up * (topH + 5f), new Vector3(length + 4f, 0.4f, totalD + 6f), Quaternion.LookRotation(outward) * Quaternion.Euler(-6f, 0, 0));
            MeshKitUnity.Spawn("Roof", roof.Build(name + "Roof"), new[] { Mat.Opaque("roof", new Color(0.82f, 0.84f, 0.88f), 0.5f, 0.3f) }, root, true, false);
            MeshKitUnity.Spawn("Structure", structure.Build(name + "Structure"), new[] { concrete, seats }, root, false, true).isStatic = true;
            for (int g = 0; g < 3; g++)
            {
                if (crowd[g].V.Count == 0) continue;
                var go = MeshKitUnity.Spawn("Crowd" + g, crowd[g].Build(name + "Crowd" + g), new[] { CrowdMaterials[g] }, root, false, false);
                CrowdGroups.Add(go.GetComponent<Renderer>());
            }
        }

        private void BuildFloodlights()
        {
            var b = new MeshBuilder(2);
            float L = P.HalfLength + 16f, W = P.HalfWidth + 16f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var basePos = new Vector3(sx * L, 0, sz * W);
                    var top = basePos + Vector3.up * 42f;
                    MeshKit.Cylinder(b, 0, basePos, top, 0.6f, 10);
                    var toCentre = (-basePos).normalized;
                    MeshKit.Box(b, 1, top + Vector3.up * 2f, new Vector3(8f, 4f, 0.5f), Quaternion.LookRotation(new Vector3(toCentre.x, -0.5f, toCentre.z)));
                }
            var lamp = Mat.Opaque("lamp", new Color(1f, 0.98f, 0.9f), 0.9f);
            Mat.SetEmission(lamp, new Color(4f, 3.9f, 3.6f));
            MeshKitUnity.Spawn("Floodlights", b.Build("Floodlights"), new[] { Mat.Opaque("tower", new Color(0.55f, 0.57f, 0.6f), 0.4f, 0.6f), lamp }, Root, false, false);
        }

        private void BuildLighting()
        {
            var sunGo = new GameObject("KeyLight");
            sunGo.transform.SetParent(Root, false);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = _o.Night ? 0.65f : 0.85f;
            Sun.intensity = _o.Night ? 1.15f : 1.3f;
            Sun.color = _o.Night ? new Color(0.95f, 0.97f, 1f) : new Color(1f, 0.96f, 0.88f);
            sunGo.transform.rotation = Quaternion.Euler(_o.Night ? 62f : 48f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = _o.Night ? new Color(0.28f, 0.32f, 0.42f) : new Color(0.55f, 0.62f, 0.75f);
            RenderSettings.ambientEquatorColor = _o.Night ? new Color(0.2f, 0.22f, 0.25f) : new Color(0.45f, 0.48f, 0.45f);
            RenderSettings.ambientGroundColor = _o.Night ? new Color(0.1f, 0.14f, 0.1f) : new Color(0.25f, 0.3f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 120f;
            RenderSettings.fogEndDistance = 420f;
            RenderSettings.fogColor = _o.Night ? new Color(0.05f, 0.07f, 0.12f) : new Color(0.62f, 0.7f, 0.8f);

            var sky = Resources.Load<Material>("Materials/NX_Sky");
            if (sky != null)
            {
                sky = new Material(sky);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", _o.Night ? new Color(0.08f, 0.1f, 0.2f) : new Color(0.5f, 0.6f, 0.8f));
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", _o.Night ? 0.25f : 1.2f);
                RenderSettings.skybox = sky;
            }
        }

        /// <summary>Animates the crowd: excitement 0..1 makes more rows stand and celebrate.</summary>
        public void AnimateCrowd(float time, float excitement)
        {
            for (int g = 0; g < CrowdMaterials.Count; g++)
            {
                var m = CrowdMaterials[g];
                float phase = Mathf.Sin(time * (2.2f + g * 0.7f) + g * 2.1f) * 0.5f + 0.5f;
                bool up = excitement > 0.75f || (excitement > 0.3f && phase < excitement);
                string prop = m.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                float bob = up ? Mathf.Abs(Mathf.Sin(time * 6f + g)) * 0.02f : 0f;
                m.SetTextureOffset(prop, new Vector2(0, (up ? 0.5f : 0f) - bob));
            }
        }
    }
}
