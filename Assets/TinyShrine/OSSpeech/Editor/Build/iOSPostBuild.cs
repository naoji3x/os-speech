#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace TinyShrine.OSSpeech.Editor.Build
{
    /// <summary>
    /// iOSビルド後処理
    /// </summary>
    public static class iOSPostBuild
    {
        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            var projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

#if UNITY_2019_3_OR_NEWER
            string main = proj.GetUnityMainTargetGuid();
            string fw = proj.GetUnityFrameworkTargetGuid();
#else
            string main = proj.TargetGuidByName(PBXProject.GetUnityTargetName());
            string fw = main;
#endif
            // 必要フレームワーク
            proj.AddFrameworkToProject(fw, "Speech.framework", false);
            proj.AddFrameworkToProject(fw, "AVFoundation.framework", false);

            // Swift設定
            proj.SetBuildProperty(fw, "SWIFT_VERSION", "5.0");
            proj.SetBuildProperty(fw, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
            proj.SetBuildProperty(main, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");

            // Info.plist 権限
            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetString("NSSpeechRecognitionUsageDescription", "音声をテキスト化するために音声認識を使用します。");
            root.SetString("NSMicrophoneUsageDescription", "音声認識にマイクを使用します。");
            plist.WriteToFile(plistPath);

            proj.WriteToFile(projPath);
        }
    }
}
#endif
