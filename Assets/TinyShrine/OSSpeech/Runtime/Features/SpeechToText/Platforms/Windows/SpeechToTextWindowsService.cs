using System;
using System.Text;
using System.Threading;

namespace TinyShrine.OSSpeech.SpeechToText
{
    /// <summary>
    /// Windows 向け STT 実装（UnityEngine.Windows.Speech の DictationRecognizer を利用）。
    /// </summary>
    public sealed class SpeechToTextWindowsService : ISpeechToTextService
    {
        private readonly StringBuilder buffer = new();
        private readonly SpeechToTextWindowsBridge bridge = new SpeechToTextWindowsBridge();
        private bool initialized;
        private string currentLocale = "ja-JP"; // Windows の DictationRecognizer はシステム言語依存で直接切替 API が無い前提

        public event Action<string> OnPartial = static _ => { };
        public event Action<string> OnFinal = static _ => { };

        public SpeechToTextWindowsService()
        {
            bridge.OnPartial += s => OnPartial?.Invoke(s);
            bridge.OnFinal += s => OnFinal?.Invoke(s);
        }

        public void Init(SynchronizationContext mainContext, string locale = "ja-JP")
        {
            if (initialized)
            {
                UnityEngine.Debug.LogWarning("[SpeechToTextWindows] Already initialized");
                return;
            }
            currentLocale = string.IsNullOrEmpty(locale) ? currentLocale : locale;
            bridge.Init(mainContext, currentLocale);
            UnityEngine.Debug.Log($"[SpeechToTextWindows] Initialized (locale={currentLocale})");
            initialized = true;
        }

        public bool Start()
        {
            if (!initialized)
            {
                UnityEngine.Debug.LogError("[SpeechToTextWindows] Not initialized");
                return false;
            }
            buffer.Clear();
            return bridge.Start();
        }

        public void Stop()
        {
            if (!initialized)
            {
                return;
            }
            bridge.Stop();
        }

        public void Dispose()
        {
            try
            {
                bridge.Dispose();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SpeechToTextWindows] Dispose failed: {ex.Message}");
            }
            initialized = false;
        }
    }
}
