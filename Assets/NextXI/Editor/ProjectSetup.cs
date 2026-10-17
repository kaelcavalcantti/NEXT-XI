using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NextXI.EditorTools
{
    /// <summary>
    /// One-click (and automatic on first open) project configuration:
    /// URP pipeline asset, material templates that force shader inclusion in builds,
    /// player settings for Android (landscape, IL2CPP, ARM64), input handling, the boot scene
    /// and build settings. Idempotent: safe to run again.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string Marker = "Assets/NextXI/Settings/.setup_done";
        private const string SettingsDir = "Assets/NextXI/Settings";
        private const string MaterialsDir = "Assets/NextXI/Resources/Materials";
        public const string BootScene = "Assets/NextXI/Scenes/Boot.unity";

        static ProjectSetup()
        {
            if (!File.Exists(Marker)) EditorApplication.delayCall += () => Run(false);
        }

        [MenuItem("NEXT XI/Configurar projeto (URP, Android, cena)")]
        public static void RunFromMenu() => Run(true);

        public static void Run(bool verbose)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                Directory.CreateDirectory(MaterialsDir);
                var urp = EnsureUrpAsset();
                EnsureMaterialTemplates(urp != null);
                ConfigurePlayer();
                EnsureBootScene();
                AssetDatabase.SaveAssets();
                File.WriteAllText(Marker, DateTime.UtcNow.ToString("o"));
                AssetDatabase.Refresh();
                Debug.Log("[NEXT XI] Projeto configurado" + (urp != null ? " com URP." : " (URP não encontrado: usando pipeline padrão)."));
                if (verbose) EditorUtility.DisplayDialog("NEXT XI", "Projeto configurado. Abra a cena Boot e aperte Play.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogError("[NEXT XI] Falha na configuração: " + e);
            }
        }

        /// <summary>Creates a URP asset (+ renderer) via reflection so this file compiles without URP.</summary>
        private static RenderPipelineAsset EnsureUrpAsset()
        {
            const string path = SettingsDir + "/NextXI_URP.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (existing != null) { Assign(existing); return existing; }
            var urpType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
            var rendererType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
            if (urpType == null || rendererType == null) return null;
            var renderer = ScriptableObject.CreateInstance(rendererType);
            AssetDatabase.CreateAsset(renderer, SettingsDir + "/NextXI_URP_Renderer.asset");
            var create = urpType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ScriptableObject).Assembly.GetType("UnityEngine.ScriptableObject") ?? typeof(ScriptableObject) }, null)
                         ?? FindCreate(urpType);
            RenderPipelineAsset asset = create != null ? (RenderPipelineAsset)create.Invoke(null, new object[] { renderer }) : (RenderPipelineAsset)ScriptableObject.CreateInstance(urpType);
            AssetDatabase.CreateAsset(asset, path);
            // Mobile-friendly defaults; QualityService tunes them at runtime.
            Set(asset, "renderScale", 0.85f);
            Set(asset, "shadowDistance", 60f);
            Set(asset, "msaaSampleCount", 2);
            Set(asset, "supportsHDR", false);
            EditorUtility.SetDirty(asset);
            Assign(asset);
            return asset;
        }

        private static MethodInfo FindCreate(Type t)
        {
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (m.Name == "Create" && m.GetParameters().Length == 1) return m;
            return null;
        }

        private static void Set(object o, string name, object value)
        {
            var p = o.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) p.SetValue(o, Convert.ChangeType(value, p.PropertyType));
        }

        private static void Assign(RenderPipelineAsset asset)
        {
            GraphicsSettings.defaultRenderPipeline = asset;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
        }

        private static void EnsureMaterialTemplates(bool urp)
        {
            CreateMat("NX_Lit", urp ? "Universal Render Pipeline/Lit" : "Standard");
            CreateMat("NX_Unlit", urp ? "Universal Render Pipeline/Unlit" : "Unlit/Texture");
            CreateMat("NX_Sky", "Skybox/Procedural");
        }

        private static void CreateMat(string name, string shader)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var sh = Shader.Find(shader);
            if (sh == null) { Debug.LogWarning("[NEXT XI] Shader não encontrado: " + shader); return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) AssetDatabase.CreateAsset(new Material(sh) { name = name }, path);
            else if (m.shader != sh) { m.shader = sh; EditorUtility.SetDirty(m); }
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "NEXT XI";
            PlayerSettings.productName = "NEXT XI";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.nextxi.game");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.nextxi.game");
            PlayerSettings.bundleVersion = NextXI.Core.Persistence.SaveSystem.GameVersion;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);
            PlayerSettings.gpuSkinning = true;
            // Legacy input is used by the touch layer: enable "Both" so it works alongside the Input System if installed.
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (ps != null && ps.Length > 0)
            {
                var so = new SerializedObject(ps[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue == 1) { prop.intValue = 2; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
        }

        private static void EnsureBootScene()
        {
            if (!File.Exists(BootScene))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BootScene));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // GameApp bootstraps itself (RuntimeInitializeOnLoadMethod); the scene only needs to exist.
                EditorSceneManager.SaveScene(scene, BootScene);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScene, true) };
        }
    }
}
