// Re-sign macOS .app automatically after each Build And Run
// Config via env vars (optional):
//   OS_SPEECH_MAC_SIGN_ID        -> codesign identity (e.g., "Developer ID Application: NAME (TEAMED)")
//                                   if unset, falls back to ad-hoc ("-")
//   OS_SPEECH_MAC_ENTITLEMENTS   -> path to entitlements.plist (optional)
//   OS_SPEECH_MAC_REMOVE_QUARANTINE -> "1" to run: xattr -dr com.apple.quarantine

using UnityEditor;
using UnityEditor.Callbacks;

namespace TinyShrine.OSSpeech.Editor.Build
{
    public static class MacCodeSignPostBuild
    {
        [PostProcessBuild(1000)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.StandaloneOSX)
            {
                return;
            }

            // pathToBuiltProject is the ".app" folder
            if (!pathToBuiltProject.EndsWith(".app", System.StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.Debug.LogWarning($"[OSSpeech] macOS post-sign skipped: not an .app: {pathToBuiltProject}");
                return;
            }

            var signId = System.Environment.GetEnvironmentVariable("OS_SPEECH_MAC_SIGN_ID");
            if (string.IsNullOrWhiteSpace(signId))
            {
                signId = "-"; // ad-hoc
            }

            var entitlements = System.Environment.GetEnvironmentVariable("OS_SPEECH_MAC_ENTITLEMENTS");
            var removeQuarantine = System.Environment.GetEnvironmentVariable("OS_SPEECH_MAC_REMOVE_QUARANTINE") == "1";

            try
            {
                if (removeQuarantine)
                {
                    Run("/usr/bin/xattr", $"-dr com.apple.quarantine \"{pathToBuiltProject}\"", "Remove quarantine");
                }

                // codesign options
                // Use Hardened Runtime only for real Developer ID signing
                var optionsRuntime = signId != "-" ? " --options runtime" : string.Empty;

                string entArg = string.Empty;
                if (!string.IsNullOrEmpty(entitlements))
                {
                    entArg = $" --entitlements \"{entitlements}\"";
                }

                Run(
                    "/usr/bin/codesign",
                    $"--force --deep --sign {EscapeIdentity(signId)}{optionsRuntime}{entArg} \"{pathToBuiltProject}\"",
                    "codesign"
                );

                // Verify
                Run(
                    "/usr/bin/codesign",
                    $"--verify --deep --strict --verbose=2 \"{pathToBuiltProject}\"",
                    "codesign verify"
                );

                UnityEngine.Debug.Log($"[OSSpeech] macOS app re-signed after build. Identity: {signId}");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[OSSpeech] macOS post-sign failed: {ex.Message}");
            }
        }

        private static string EscapeIdentity(string identity)
        {
            // When identity contains spaces, wrap in quotes unless it's ad-hoc "-"
            if (identity == "-")
            {
                return identity;
            }
            return $"\"{identity}\"";
        }

        private static void Run(string fileName, string args, string label)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null)
            {
                throw new System.InvalidOperationException($"Failed to start process: {fileName}");
            }

            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                UnityEngine.Debug.Log($"[OSSpeech:{label}] {stdout.Trim()}");
            }
            if (proc.ExitCode != 0)
            {
                throw new System.InvalidOperationException($"{label} failed (exit {proc.ExitCode}): {stderr.Trim()}");
            }
        }
    }
}
