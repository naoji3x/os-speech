using System;
using System.Threading;

namespace TinyShrine.OSSpeech.SpeechToText
{
    public sealed class SpeechToTextWindowsBridge
    {
        public event Action<string> OnPartial = static _ => { };
        public event Action<string> OnFinal = static _ => { };

#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
        private UnityEngine.Windows.Speech.DictationRecognizer? recognizer;
#endif

        private bool initialized;
        private bool listening;
        private string currentLocale = "ja-JP";

        public void Init(SynchronizationContext mainContext, string locale = "ja-JP")
        {
            if (initialized)
            {
                UnityEngine.Debug.LogWarning("[WinBridge] Already initialized");
                return;
            }
            currentLocale = string.IsNullOrEmpty(locale) ? currentLocale : locale;

#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
            try
            {
                recognizer = new UnityEngine.Windows.Speech.DictationRecognizer(
                    UnityEngine.Windows.Speech.ConfidenceLevel.Medium
                );
                recognizer.DictationHypothesis += text => OnPartial?.Invoke(text);
                recognizer.DictationResult += (text, confidence) => OnFinal?.Invoke(text);
                recognizer.DictationError += (error, hResult) =>
                {
                    UnityEngine.Debug.LogError($"[WinBridge] Error: {error} (0x{hResult:X8})");
                };
                initialized = true;
                UnityEngine.Debug.Log($"[WinBridge] Initialized (locale={currentLocale})");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WinBridge] Init failed: {ex.Message}");
                recognizer = null;
                initialized = false;
            }
#else
            UnityEngine.Debug.LogWarning("[WinBridge] Windows speech API is not available on this platform.");
#endif
        }

        public bool Start()
        {
            if (!initialized)
            {
                UnityEngine.Debug.LogError("[WinBridge] Not initialized");
                return false;
            }
#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
            if (listening)
                return true;
            try
            {
                recognizer?.Start();
                listening = true;
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WinBridge] Start failed: {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        public void Stop()
        {
            if (!initialized || !listening)
            {
                return;
            }
#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
            try
            {
                recognizer?.Stop();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WinBridge] Stop failed: {ex.Message}");
            }
            finally
            {
                listening = false;
            }
#endif
        }

        public void Dispose()
        {
#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
            try
            {
                if (recognizer != null)
                {
                    if (listening)
                    {
                        recognizer.Stop();
                        listening = false;
                    }
                    recognizer.Dispose();
                    recognizer = null;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[WinBridge] Dispose failed: {ex.Message}");
            }
#endif
            initialized = false;
        }
    }
}
