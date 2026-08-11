using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace WavePrototype.Editor
{
    public static partial class BatchBuild
    {
        [MenuItem("Wave Prototype/Build Batch 3 Windows")]
        public static void BuildBatch3()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch3/TacticalSailingBatch3.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 4 Windows")]
        public static void BuildBatch4()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch4/TacticalSailingBatch4.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=4: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 5 Windows")]
        public static void BuildBatch5()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch5/TacticalSailingBatch5.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=5: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 6 Windows")]
        public static void BuildBatch6()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch6/TacticalSailingBatch6.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=6: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 7 Windows")]
        public static void BuildBatch7()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch7/TacticalSailingBatch7.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=7: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 8 Windows")]
        public static void BuildBatch8()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch8/TacticalSailingBatch8.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=8: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 9 Windows")]
        public static void BuildBatch9()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch9/TacticalSailingBatch9.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=9: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 10 Windows")]
        public static void BuildBatch10()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch10/TacticalSailingBatch10.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=10: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 11 Windows")]
        public static void BuildBatch11()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch11/TacticalSailingBatch11.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=11: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 12 Windows")]
        public static void BuildBatch12()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch12/TacticalSailingBatch12.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=12: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 13 Windows")]
        public static void BuildBatch13()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch13/TacticalSailingBatch13.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=13: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 14 Windows")]
        public static void BuildBatch14()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch14/TacticalSailingBatch14.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=14: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 15 Windows")]
        public static void BuildBatch15()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch15/TacticalSailingBatch15.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=15: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 16 Windows")]
        public static void BuildBatch16()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch16/TacticalSailingBatch16.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=16: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 17 Windows")]
        public static void BuildBatch17()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch17/TacticalSailingBatch17.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=17: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        [MenuItem("Wave Prototype/Build Batch 18 Windows")]
        public static void BuildBatch18()
        {
            try
            {
                RunValidation();
                EnsureScene();
                string output = Path.GetFullPath("Builds/Batch18/TacticalSailingBatch18.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.StrictMode
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows build failed: " + report.summary.result);
                Debug.Log($"[WAVE-BUILD] SUCCESS batch=18: {output} ({report.summary.totalSize:N0} bytes, {report.summary.totalTime})");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        // Kept as a stable command-line alias for existing automation.
        public static void BuildWindows() => BuildBatch18();

        private static void EnsureScene()
        {
            Directory.CreateDirectory("Assets/WavePrototype");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
        }

    }
}
