using System.Collections.Generic;
using UnityEngine;

namespace NextXI.Runtime
{
    /// <summary>Accumulates geometry with optional submeshes and builds a Unity mesh.</summary>
    public sealed class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<BoneWeight> W = new List<BoneWeight>();
        public readonly List<List<int>> Sub = new List<List<int>>();
        public bool Skinned;

        public MeshBuilder(int submeshes = 1)
        {
            for (int i = 0; i < submeshes; i++) Sub.Add(new List<int>());
        }

        public int Add(Vector3 v, Vector3 n, Vector2 uv)
        {
            V.Add(v); N.Add(n); UV.Add(uv);
            if (Skinned) W.Add(new BoneWeight { boneIndex0 = 0, weight0 = 1f });
            return V.Count - 1;
        }

        public void Tri(int sub, int a, int b, int c) { var l = Sub[sub]; l.Add(a); l.Add(b); l.Add(c); }
        public void Quad(int sub, int a, int b, int c, int d) { Tri(sub, a, b, c); Tri(sub, a, c, d); }

        /// <summary>
        /// Debug validation: Unity front faces are clockwise, i.e. cross(b-a, c-a) must point
        /// along the vertex normals. Returns the number of triangles that violate this.
        /// </summary>
        public int CountInvertedTriangles()
        {
            int bad = 0;
            foreach (var list in Sub)
                for (int i = 0; i + 2 < list.Count; i += 3)
                {
                    Vector3 a = V[list[i]], b = V[list[i + 1]], c = V[list[i + 2]];
                    Vector3 fn = Vector3.Cross(b - a, c - a);
                    Vector3 vn = N[list[i]] + N[list[i + 1]] + N[list[i + 2]];
                    if (fn.sqrMagnitude > 1e-12f && Vector3.Dot(fn, vn) < 0f) bad++;
                }
            return bad;
        }

        public Mesh Build(string name, bool recalcNormals = false)
        {
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(V);
            m.SetNormals(N);
            m.SetUVs(0, UV);
            if (Skinned) m.boneWeights = W.ToArray();
            m.subMeshCount = Sub.Count;
            for (int i = 0; i < Sub.Count; i++) m.SetTriangles(Sub[i], i);
            if (recalcNormals) m.RecalculateNormals();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int bad = CountInvertedTriangles();
            if (bad > 0) Debug.LogWarning($"[NEXT XI][Mesh] {name}: {bad} triangles wound against their normals");
#endif
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }
}
