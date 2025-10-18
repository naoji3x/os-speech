using System;
using System.Threading;

namespace TinyShrine.OSSpeech.SpeechToText
{
    public sealed class SpeechToTextWindowsBridge : IDisposable
    {
        public event Action<string> OnPartial = static _ => { };
        public event Action<string> OnFinal = static _ => { };

#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
        private UnityEngine.Windows.Speech.DictationRecognizer? recognizer;
        private bool listening;
        private bool initialized;
        private string currentLocale = "ja-JP";
#endif

        public void Init(SynchronizationContext mainContext, string locale = "ja-JP")
        {
#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
            if (initialized)
            {
                UnityEngine.Debug.LogWarning("[SpeechToTextWindowsBridge] Already initialized");
                return;
            }
            currentLocale = string.IsNullOrEmpty(locale) ? currentLocale : locale;

            try
            {
                recognizer = new UnityEngine.Windows.Speech.DictationRecognizer(
                    UnityEngine.Windows.Speech.ConfidenceLevel.Medium
                );
                recognizer.DictationHypothesis += text => OnPartial?.Invoke(text);
                recognizer.DictationResult += (text, confidence) => OnFinal?.Invoke(text);
                recognizer.DictationError += (error, hResult) =>
                {
                    UnityEngine.Debug.LogError($"[SpeechToTextWindowsBridge] Error: {error} (0x{hResult:X8})");
                };
                initialized = true;
                UnityEngine.Debug.Log($"[SpeechToTextWindowsBridge] Initialized (locale={currentLocale})");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SpeechToTextWindowsBridge] Init failed: {ex.Message}");
                recognizer = null;
                initialized = false;
            }
#else
            UnityEngine.Debug.LogWarning(
                "[SpeechToTextWindowsBridge] Windows speech API is not available on this platform."
            );
#endif
        }

        public bool Start()
        {
#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
            if (!initialized)
            {
                UnityEngine.Debug.LogError("[SpeechToTextWindowsBridge] Not initialized");
                return false;
            }
            if (listening)
            {
                return true;
            }

            try
            {
                recognizer?.Start();
                listening = true;
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SpeechToTextWindowsBridge] Start failed: {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        public void Stop()
        {
#if UNITY_STANDALONE_WIN || UNITY_WSA || UNITY_EDITOR_WIN
            if (!initialized || !listening)
            {
                return;
            }
            try
            {
                recognizer?.Stop();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SpeechToTextWindowsBridge] Stop failed: {ex.Message}");
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
                UnityEngine.Debug.LogError($"[SpeechToTextWindowsBridge] Dispose failed: {ex.Message}");
            }
            initialized = false;
#endif
        }
    }
}
