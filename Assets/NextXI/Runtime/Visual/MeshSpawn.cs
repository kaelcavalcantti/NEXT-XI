using UnityEngine;

namespace NextXI.Runtime
{
    public static partial class MeshKitUnity
    {
        public static GameObject Spawn(string name, Mesh mesh, Material[] mats, Transform parent, bool castShadows = true, bool receiveShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = receiveShadows;
            return go;
        }
    }
}
