using System;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.SpeechToText
{
    public class SpeechToTextAppleService : ISpeechToTextService
    {
        private SpeechToTextAppleBridge? appleBridge;

        /// <summary>途中経過（部分結果）。UIに逐次表示したいときに。</summary>
        public event Action<string> OnPartial = text => { };

        /// <summary>確定結果。DB保存やLLM投入などはこちらで。</summary>
        public event Action<string> OnFinal = text => { };

        public void Init(SynchronizationContext mainContext, string locale = "ja-JP")
        {
            if (appleBridge != null)
            {
                Debug.LogWarning(
                    "[SpeechToTextAppleService] Already initialized. Call Dispose() first if you want to reinitialize."
                );
                return;
            }

            try
            {
                // Apple Bridge を初期化
                appleBridge = new SpeechToTextAppleBridge(mainContext, locale);

                // イベントハンドラーを設定
                appleBridge.OnPartial += text => OnPartial?.Invoke(text);
                appleBridge.OnFinal += text => OnFinal?.Invoke(text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpeechToTextAppleService] Initialization failed: {ex.Message}");
                Dispose();
            }
        }

        public bool Start()
        {
            if (appleBridge == null)
            {
                Debug.LogError("[SpeechToTextAppleService] Not initialized. Call Init() first.");
                return false;
            }

            return appleBridge.Start();
        }

        public void Stop()
        {
            if (appleBridge == null)
            {
                return;
            }

            appleBridge.Stop();
        }

        public void Dispose()
        {
            try
            {
                Stop();
                appleBridge?.Dispose();
                appleBridge = null;
                Debug.Log("[SpeechToTextAppleService] Disposed.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAppleService] Error during disposal: {e.Message}");
            }
            GC.SuppressFinalize(this);
        }
    }
}
