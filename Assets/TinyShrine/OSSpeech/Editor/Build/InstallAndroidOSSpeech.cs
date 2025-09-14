using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TinyShrine.OSSpeech.Editor.Build
{
    public static class InstallAndroidOSSpeech
    {
        private const string RelPluginDir = "plugins/android/osspeech"; // リポ内の相対パス
        private const string DestDir = "Assets/Plugins/Android";

        [MenuItem("Tools/Plugins/Android/Build & Install osspeech")]
        public static void BuildAndInstall()
        {
            var repoRoot = Directory.GetParent(Application.dataPath)!.FullName;
            var workDir = Path.Combine(repoRoot, RelPluginDir);

            UnityEngine.Debug.Log($"WorkingDir: {workDir}");

            // 1) gradlew があれば使う。なければ gradle を試す
            string shell;
            string args;
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                shell = "cmd.exe";
                args = File.Exists(Path.Combine(workDir, "gradlew.bat"))
                    ? "/c gradlew.bat assembleRelease"
                    : "/c gradle assembleRelease";
            }
            else
            {
                shell = "/bin/sh";
                args = File.Exists(Path.Combine(workDir, "gradlew"))
                    ? "-lc ./gradlew assembleRelease"
                    : "-lc gradle assembleRelease";
            }

            UnityEngine.Debug.Log($"Shell: {shell} {args}");

            var psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = args,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

#if UNITY_EDITOR_OSX
            const string jdk = "/Applications/Android Studio.app/Contents/jbr/Contents/Home";
#elif UNITY_EDITOR_WIN
            const string jdk = @"C:\Program Files\Android\Android Studio\jbr";
#else
            const string jdk = System.Environment.GetEnvironmentVariable("JAVA_HOME");
#endif

            psi.EnvironmentVariables["JAVA_HOME"] = jdk;
            psi.EnvironmentVariables["GRADLE_JAVA_HOME"] = jdk;
            // PATH 先頭に jdk/bin を追加（スペースを含んでもOK）
            psi.EnvironmentVariables["PATH"] =
                (Application.platform == RuntimePlatform.WindowsEditor ? jdk + @"\bin;" : jdk + "/bin:")
                + (psi.EnvironmentVariables.ContainsKey("PATH") ? psi.EnvironmentVariables["PATH"] : string.Empty);

            using var p = Process.Start(psi);

            p.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    UnityEngine.Debug.Log(e.Data);
                }
            };
            p.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    UnityEngine.Debug.LogError(e.Data);
                }
            };

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                EditorUtility.DisplayDialog("Gradle", "assembleRelease 失敗", "OK");
                return;
            }

            // 2) 最新の AAR をコピー
            var aarDir = Path.Combine(workDir, "build/outputs/aar");
            var aar = Directory.Exists(aarDir)
                ? Directory.GetFiles(aarDir, "*.aar").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
                : null;
            if (aar == null)
            {
                EditorUtility.DisplayDialog("Copy", "AARが見つかりません", "OK");
                return;
            }

            Directory.CreateDirectory(DestDir);
            var dest = Path.Combine(Directory.GetCurrentDirectory(), DestDir, Path.GetFileName(aar));
            File.Copy(aar, dest, true);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Done", $"Installed: {Path.GetFileName(dest)}", "OK");
        }
    }
}
