using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine; // mac でも使えます

namespace TinyShrine.OSSpeech.Editor.Build
{
    public static class MacPostBuild
    {
        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.StandaloneOSX)
            {
                return;
            }

            var plistPath = Path.Combine(pathToBuiltProject, "Contents", "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            var root = plist.root;
            root.SetString("NSMicrophoneUsageDescription", "音声入力にマイクを使用します。");
            root.SetString("NSSpeechRecognitionUsageDescription", "音声認識を行うために音声を処理します。");

            File.WriteAllText(plistPath, plist.WriteToString());
            Debug.Log("[OSSpeech] Updated macOS Info.plist");
        }
    }
}
