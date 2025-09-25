using System;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.SpeechToText
{
    public class SpeechToTextAppleService : ISpeechToTextService
    {
        private SpeechToTextAppleBridge? bridge;

        /// <summary>途中経過（部分結果）。UIに逐次表示したいときに。</summary>
        public event Action<string> OnPartial = text => { };

        /// <summary>確定結果。DB保存やLLM投入などはこちらで。</summary>
        public event Action<string> OnFinal = text => { };

        public void Init(SynchronizationContext mainContext, string locale = "ja-JP")
        {
            if (bridge != null)
            {
                Debug.LogWarning(
                    "[SpeechToTextAppleService] Already initialized. Call Dispose() first if you want to reinitialize."
                );
                return;
            }

            try
            {
                // Apple Bridge を初期化
                bridge = new SpeechToTextAppleBridge(mainContext, locale);

                // イベントハンドラーを設定
                bridge.OnPartial += text => OnPartial?.Invoke(text);
                bridge.OnFinal += text => OnFinal?.Invoke(text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SpeechToTextAppleService] Initialization failed: {ex.Message}");
                Dispose();
            }
        }

        public bool Start()
        {
            if (bridge == null)
            {
                Debug.LogError("[SpeechToTextAppleService] Not initialized. Call Init() first.");
                return false;
            }

            return bridge.Start();
        }

        public void Stop()
        {
            if (bridge == null)
            {
                return;
            }

            bridge.Stop();
        }

        public void Dispose()
        {
            try
            {
                Stop();
                bridge?.Dispose();
                bridge = null;
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
