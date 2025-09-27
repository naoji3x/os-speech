using System;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.TextToSpeech
{
    public class TextToSpeechAppleService : ITextToSpeechService, IDisposable
    {
        private TextToSpeechAppleBridge? bridge;
        private bool disposed;

        public event Action OnStart = static () => { };
        public event Action OnFinish = static () => { };
        public event Action OnCancel = static () => { };
        public event Action OnError = static () => { };

        public void Init(SynchronizationContext mainContext, string locale = "ja-JP")
        {
            if (bridge != null)
            {
                Debug.LogWarning(
                    "[TextToSpeechAppleService] すでに初期化されています。再初期化する場合は先にDispose()を呼んでください。"
                );
                return;
            }
            try
            {
                // Apple Bridge を初期化
                bridge = new TextToSpeechAppleBridge(mainContext, locale);

                // イベントハンドラーを設定
                bridge.OnStart += () => OnStart?.Invoke();
                bridge.OnFinish += () => OnFinish?.Invoke();
                bridge.OnCancel += () => OnCancel?.Invoke();
                bridge.OnError += () => OnError?.Invoke();

                // ロケールを設定
                SetLocale(locale);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TextToSpeechAppleService] 初期化に失敗しました: {ex.Message}");
                bridge = null;
            }
        }

        public void SetLocale(string locale)
        {
            if (!EnsureReady())
            {
                return;
            }
            bridge!.SetLocale(locale);
        }

        public void SetVoiceId(string identifierOrNull)
        {
            if (!EnsureReady())
            {
                return;
            }
            bridge!.SetVoiceId(identifierOrNull);
        }

        public bool Speak(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f,
            bool queue = false
        )
        {
            if (!EnsureReady())
            {
                return false;
            }
            return bridge!.Speak(text, rate01, pitch, volume01, queue);
        }

        public void Stop()
        {
            if (!EnsureReady())
            {
                return;
            }
            bridge!.Stop();
        }

        public bool IsSpeaking()
        {
            if (!EnsureReady())
            {
                return false;
            }
            return bridge!.IsSpeaking();
        }

        public string? ListVoicesJson()
        {
            if (!EnsureReady())
            {
                return null;
            }
            return bridge!.ListVoicesJson();
        }

        public AudioClip? SynthesizeToClip(string text, float rate01 = 1.0f, float pitch = 1.0f, float volume01 = 1.0f)
        {
            if (!EnsureReady())
            {
                return null;
            }
            return bridge!.SynthesizeToClip(text, rate01, pitch, volume01);
        }

        /// <summary>
        /// リソース解放。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }
            if (disposing)
            {
                try
                {
                    // 話中であれば停止
                    if (bridge != null && bridge.IsSpeaking())
                    {
                        bridge.Stop();
                    }
                    bridge?.Dispose();
                    bridge = null;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TextToSpeechAppleService] Dispose に失敗しました: {e.Message}");
                }
            }
            disposed = true;
        }

        private bool EnsureReady()
        {
            if (disposed)
            {
                Debug.LogError("[TextToSpeechAppleService] 既に Dispose 済みです。");
                return false;
            }
            if (bridge == null)
            {
                Debug.LogError("[TextToSpeechAppleService] Init() がまだ呼ばれていません。");
                return false;
            }
            return true;
        }
    }
}
