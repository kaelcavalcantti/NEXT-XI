using System.Collections.Generic;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Primitive generators (all procedural, no imported art).</summary>
    public static class MeshKit
    {
        public static void Box(MeshBuilder b, int sub, Vector3 center, Vector3 size, Quaternion rot, Vector2 uvScale = default)
        {
            if (uvScale == default) uvScale = Vector2.one;
            Vector3 h = size * 0.5f;
            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
            foreach (var n in dirs)
            {
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n);
                Vector3 v = Vector3.Cross(n, u);
                Vector3 c = Vector3.Scale(n, h);
                float su = Mathf.Abs(Vector3.Dot(u, size)), sv = Mathf.Abs(Vector3.Dot(v, size));
                Vector3 hu = u * su * 0.5f, hv = v * sv * 0.5f;
                Vector3 rn = rot * n;
                int i0 = b.Add(center + rot * (c - hu - hv), rn, new Vector2(0, 0));
                int i1 = b.Add(center + rot * (c + hu - hv), rn, new Vector2(su * uvScale.x, 0));
                int i2 = b.Add(center + rot * (c + hu + hv), rn, new Vector2(su * uvScale.x, sv * uvScale.y));
                int i3 = b.Add(center + rot * (c - hu + hv), rn, new Vector2(0, sv * uvScale.y));
                // Unity front faces are clockwise as seen from outside.
                b.Tri(sub, i0, i1, i2);
                b.Tri(sub, i0, i2, i3);
            }
        }

        /// <summary>Cylinder between two points.</summary>
        public static void Cylinder(MeshBuilder b, int sub, Vector3 a, Vector3 c, float r, int seg = 12, bool caps = true)
        {
            Vector3 axis = (c - a).normalized;
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            int start = b.V.Count;
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg * Mathf.PI * 2;
                Vector3 n = u * Mathf.Cos(t) + v * Mathf.Sin(t);
                b.Add(a + n * r, n, new Vector2(i / (float)seg, 0));
                b.Add(c + n * r, n, new Vector2(i / (float)seg, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int i0 = start + i * 2;
                b.Tri(sub, i0, i0 + 3, i0 + 1);
                b.Tri(sub, i0, i0 + 2, i0 + 3);
            }
            if (!caps) return;
            foreach (var (p, n) in new[] { (a, -axis), (c, axis) })
            {
                int center = b.Add(p, n, new Vector2(0.5f, 0.5f));
                int ring = b.V.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float t = i / (float)seg * Mathf.PI * 2;
                    b.Add(p + (u * Mathf.Cos(t) + v * Mathf.Sin(t)) * r, n, new Vector2(0.5f + Mathf.Cos(t) * 0.5f, 0.5f + Mathf.Sin(t) * 0.5f));
                }
                for (int i = 0; i < seg; i++)
                {
                    if (n == axis) b.Tri(sub, center, ring + i, ring + i + 1);
                    else b.Tri(sub, center, ring + i + 1, ring + i);
                }
            }
        }

        public static void Sphere(MeshBuilder b, int sub, Vector3 center, Vector3 radii, int lon = 24, int lat = 14)
        {
            int start = b.V.Count;
            for (int y = 0; y <= lat; y++)
            {
                float v = y / (float)lat;
                float th = v * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float u = x / (float)lon;
                    float ph = u * Mathf.PI * 2;
                    var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    var p = Vector3.Scale(n, radii);
                    var nn = new Vector3(n.x / radii.x, n.y / radii.y, n.z / radii.z).normalized;
                    b.Add(center + p, nn, new Vector2(u, 1 - v));
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i0 = start + y * (lon + 1) + x, i1 = i0 + lon + 1;
                    b.Tri(sub, i0, i0 + 1, i1);
                    b.Tri(sub, i0 + 1, i1 + 1, i1);
                }
        }

        /// <summary>Flat quad on the XZ plane (y up), used for pitch markings and decals.</summary>
        public static void FlatQuad(MeshBuilder b, int sub, Vector3 a, Vector3 c, float width, float y, Vector2 uv0 = default, Vector2 uv1 = default)
        {
            if (uv1 == default) uv1 = Vector2.one;
            Vector3 d = (c - a); d.y = 0;
            Vector3 side = Vector3.Cross(Vector3.up, d.normalized) * width * 0.5f;
            a.y = y; c.y = y;
            int i0 = b.Add(a - side, Vector3.up, new Vector2(uv0.x, uv0.y));
            int i1 = b.Add(a + side, Vector3.up, new Vector2(uv1.x, uv0.y));
            int i2 = b.Add(c + side, Vector3.up, new Vector2(uv1.x, uv1.y));
            int i3 = b.Add(c - side, Vector3.up, new Vector2(uv0.x, uv1.y));
            b.Tri(sub, i0, i2, i1);
            b.Tri(sub, i0, i3, i2);
        }

        /// <summary>Arc of a flat line (circle segments) on the ground.</summary>
        public static void FlatArc(MeshBuilder b, int sub, Vector3 center, float radius, float a0, float a1, float width, float y, int seg = 48)
        {
            int start = b.V.Count;
            for (int i = 0; i <= seg; i++)
            {
                float t = Mathf.Lerp(a0, a1, i / (float)seg);
                var dir = new Vector3(Mathf.Cos(t), 0, Mathf.Sin(t));
                b.Add(center + dir * (radius - width * 0.5f) + Vector3.up * y, Vector3.up, new Vector2(0, i / (float)seg));
                b.Add(center + dir * (radius + width * 0.5f) + Vector3.up * y, Vector3.up, new Vector2(1, i / (float)seg));
            }
            for (int i = 0; i < seg; i++)
            {
                int i0 = start + i * 2;
                b.Tri(sub, i0, i0 + 2, i0 + 1);
                b.Tri(sub, i0 + 1, i0 + 2, i0 + 3);
            }
        }

        /// <summary>Vertical quad (both faces) from a to c with given height.</summary>
        public static void Wall(MeshBuilder b, int sub, Vector3 a, Vector3 c, float y0, float y1, Vector2 uvScale, bool doubleSided)
        {
            Vector3 d = c - a; d.y = 0;
            Vector3 n = Vector3.Cross(d.normalized, Vector3.up);
            float len = d.magnitude;
            int i0 = b.Add(new Vector3(a.x, y0, a.z), n, new Vector2(0, 0));
            int i1 = b.Add(new Vector3(c.x, y0, c.z), n, new Vector2(len * uvScale.x, 0));
            int i2 = b.Add(new Vector3(c.x, y1, c.z), n, new Vector2(len * uvScale.x, (y1 - y0) * uvScale.y));
            int i3 = b.Add(new Vector3(a.x, y1, a.z), n, new Vector2(0, (y1 - y0) * uvScale.y));
            b.Tri(sub, i0, i1, i2); b.Tri(sub, i0, i2, i3);
            if (!doubleSided) return;
            int j0 = b.Add(new Vector3(a.x, y0, a.z), -n, new Vector2(0, 0));
            int j1 = b.Add(new Vector3(c.x, y0, c.z), -n, new Vector2(len * uvScale.x, 0));
            int j2 = b.Add(new Vector3(c.x, y1, c.z), -n, new Vector2(len * uvScale.x, (y1 - y0) * uvScale.y));
            int j3 = b.Add(new Vector3(a.x, y1, a.z), -n, new Vector2(0, (y1 - y0) * uvScale.y));
            b.Tri(sub, j0, j2, j1); b.Tri(sub, j0, j3, j2);
        }
    }
}
