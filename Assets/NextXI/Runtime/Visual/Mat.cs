using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NextXI.Runtime
{
    /// <summary>
    /// Material factory. Uses the URP Lit shader when URP is active, otherwise the built-in
    /// Standard shader. Materials are cached by key to keep the number of materials (and
    /// SRP batches) low on mobile.
    /// </summary>
    public static class Mat
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        private static Shader _lit, _unlit, _transparent;

        public static bool IsUrp => GraphicsSettings.currentRenderPipeline != null;

        private static Shader Lit
        {
            get
            {
                if (_lit != null) return _lit;
                var template = Resources.Load<Material>("Materials/NX_Lit");
                if (template != null) _lit = template.shader;
                if (_lit == null) _lit = Shader.Find(IsUrp ? "Universal Render Pipeline/Lit" : "Standard");
                if (_lit == null) _lit = Shader.Find("Standard");
                return _lit;
            }
        }

        private static Shader Unlit
        {
            get
            {
                if (_unlit != null) return _unlit;
                var template = Resources.Load<Material>("Materials/NX_Unlit");
                if (template != null) _unlit = template.shader;
                if (_unlit == null) _unlit = Shader.Find(IsUrp ? "Universal Render Pipeline/Unlit" : "Unlit/Texture");
                if (_unlit == null) _unlit = Shader.Find("Unlit/Color");
                return _unlit;
            }
        }

        public static Material Opaque(string key, Color color, float smoothness = 0.3f, float metallic = 0f, Texture tex = null)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Lit) { name = "NX_" + key, enableInstancing = true };
            SetColor(m, color);
            if (tex != null) SetTexture(m, tex);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            Cache[key] = m;
            return m;
        }

        public static Material UnlitTex(string key, Texture tex, Color color)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Unlit) { name = "NX_" + key };
            SetColor(m, color);
            SetTexture(m, tex);
            Cache[key] = m;
            return m;
        }

        /// <summary>Alpha-tested material (nets, numbers, crowd cards).</summary>
        public static Material Cutout(string key, Texture tex, Color color, bool doubleSided = true)
        {
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Lit) { name = "NX_" + key, enableInstancing = true };
            SetColor(m, color);
            SetTexture(m, tex);
            if (IsUrp)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                if (doubleSided && m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            }
            else
            {
                m.SetFloat("_Mode", 1f);
                m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            Cache[key] = m;
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static void SetTexture(Material m, Texture t)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        }

        public static void SetEmission(Material m, Color c)
        {
            if (!m.HasProperty("_EmissionColor")) return;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }

        public static void ClearCache()
        {
            foreach (var m in Cache.Values) if (m != null) Object.Destroy(m);
            Cache.Clear();
        }
    }
}
