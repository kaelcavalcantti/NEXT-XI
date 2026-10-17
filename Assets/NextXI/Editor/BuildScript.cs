using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NextXI.EditorTools
{
    /// <summary>
    /// Command-line builds, e.g.:
    /// Unity -batchmode -quit -projectPath . -executeMethod NextXI.EditorTools.BuildScript.BuildAndroidApk -logFile build.log
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("NEXT XI/Build/Android APK (desenvolvimento)")]
        public static void BuildAndroidApk() => Build(false, true);

        [MenuItem("NEXT XI/Build/Android AAB (Play Store)")]
        public static void BuildAndroidAab() => Build(true, false);

        private static void Build(bool aab, bool development)
        {
            ProjectSetup.Run(false);
            Directory.CreateDirectory("Builds/Android");
#if !UNITY_EDITOR_CHECK // API newer than the reference assembly used for offline compile checks
            EditorUserBuildSettings.buildAppBundle = aab;
#endif
            string path = $"Builds/Android/NEXT_XI_{NextXI.Core.Persistence.SaveSystem.GameVersion}{(development ? "_dev" : "")}.{(aab ? "aab" : "apk")}";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.BootScene },
                locationPathName = path,
                target = BuildTarget.Android,
                options = development ? BuildOptions.Development : BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[NEXT XI] Build {s.result}: {path} ({s.totalSize / (1024 * 1024)} MB) em {s.totalTime}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Runs EditMode tests from the command line (see Docs/TESTING.md).</summary>
        public static void PrintTestHelp() =>
            Debug.Log("Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml");
    }
}
