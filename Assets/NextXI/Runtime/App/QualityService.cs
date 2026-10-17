using System;
using System.Reflection;
using NextXI.Core.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsSettings = NextXI.Core.Settings.GraphicsSettings;

namespace NextXI.Runtime
{
    /// <summary>
    /// Applies graphics profiles (Economy / Balanced / High / Custom): frame rate target, render
    /// scale, shadows, MSAA, crowd density. Talks to the URP asset through reflection so the
    /// runtime also works (with reduced options) on the built-in pipeline. Includes a simple
    /// dynamic-resolution controller that lowers render scale under sustained frame drops.
    /// </summary>
    public sealed class QualityService
    {
        public GraphicsSettings Current { get; private set; } = new GraphicsSettings();
        private float _dynScale = 1f;
        private float _slowTime, _fastTime;

        public void Apply(GraphicsSettings g)
        {
            Current = g;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = g.TargetFps;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _dynScale = 1f;
            var rp = GraphicsSettings_CurrentPipeline();
            if (rp != null)
            {
                SetProp(rp, "renderScale", g.RenderScale);
                SetProp(rp, "shadowDistance", (float)(g.Shadows ? g.ShadowDistance : 0));
                SetProp(rp, "msaaSampleCount", Math.Max(1, g.AntiAliasing));
                SetProp(rp, "supportsHDR", g.PostProcessing);
            }
            else
            {
                QualitySettings.shadows = g.Shadows ? ShadowQuality.All : ShadowQuality.Disable;
                QualitySettings.shadowDistance = g.ShadowDistance;
                QualitySettings.antiAliasing = g.AntiAliasing;
            }
            QualitySettings.lodBias = g.Profile == QualityProfile.Economy ? 0.6f : g.Profile == QualityProfile.High ? 1.5f : 1f;
            Log("applied " + g.Profile + $" fps={g.TargetFps} scale={g.RenderScale} shadows={g.Shadows}");
        }

        private static RenderPipelineAsset GraphicsSettings_CurrentPipeline()
        {
            var rp = QualitySettings.renderPipeline;
            if (rp == null) rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            return rp;
        }

        private static void SetProp(object o, string name, object value)
        {
            try
            {
                var p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.CanWrite) p.SetValue(o, Convert.ChangeType(value, p.PropertyType));
            }
            catch (Exception e) { Debug.LogWarning("[NEXT XI][Quality] " + name + ": " + e.Message); }
        }

        /// <summary>Call every frame during matches.</summary>
        public void Tick(float unscaledDt)
        {
            if (!Current.DynamicResolution) return;
            float target = 1f / Current.TargetFps;
            if (unscaledDt > target * 1.25f) { _slowTime += unscaledDt; _fastTime = 0; }
            else if (unscaledDt < target * 1.05f) { _fastTime += unscaledDt; _slowTime = 0; }
            if (_slowTime > 2f && _dynScale > 0.7f) { _dynScale -= 0.1f; _slowTime = 0; Push(); }
            else if (_fastTime > 6f && _dynScale < 1f) { _dynScale += 0.05f; _fastTime = 0; Push(); }
        }

        private void Push()
        {
            var rp = GraphicsSettings_CurrentPipeline();
            if (rp != null) SetProp(rp, "renderScale", Mathf.Clamp(Current.RenderScale * _dynScale, 0.5f, 1f));
            Log($"dynamic resolution x{_dynScale:0.00}");
        }

        private static void Log(string s) => Debug.Log("[NEXT XI][Quality] " + s);
    }
}
