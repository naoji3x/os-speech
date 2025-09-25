using System;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.TextToSpeech
{
    /// <summary>
    /// Android 向け TextToSpeech サービス実装。<see cref="TextToSpeechAndroidBridge"/> をラップし ITextToSpeechService を提供します。
    /// </summary>
    public class TextToSpeechAndroidService : ITextToSpeechService, IDisposable
    {
        private TextToSpeechAndroidBridge? bridge;
        private bool disposed;

        public event Action OnStart = static () => { };
        public event Action OnFinish = static () => { };
        public event Action OnCancel = static () => { };
        public event Action OnError = static () => { };

        /// <inheritdoc />
        public void Init(SynchronizationContext mainContext, string locale = "ja-JP")
        {
            if (bridge != null)
            {
                Debug.LogWarning(
                    "[TextToSpeechAndroidService] すでに初期化済みです。再初期化する場合は Dispose() してください。"
                );
                return;
            }
            try
            {
                bridge = new TextToSpeechAndroidBridge(mainContext, locale);
                bridge.OnStart += () => OnStart?.Invoke();
                bridge.OnFinish += () => OnFinish?.Invoke();
                bridge.OnCancel += () => OnCancel?.Invoke();
                bridge.OnError += () => OnError?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TextToSpeechAndroidService] 初期化に失敗: {ex.Message}");
                bridge = null;
            }
        }

        /// <inheritdoc />
        public void SetLanguage(string lang)
        {
            if (!EnsureReady())
            {
                return;
            }

            bridge!.SetLanguage(lang);
        }

        /// <inheritdoc />
        public void SetVoiceId(string identifierOrNull)
        {
            if (!EnsureReady())
            {
                return;
            }

            bridge!.SetVoiceId(identifierOrNull);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void Stop()
        {
            if (!EnsureReady())
            {
                return;
            }

            bridge!.Stop();
        }

        /// <inheritdoc />
        public bool IsSpeaking()
        {
            if (!EnsureReady())
            {
                return false;
            }

            return bridge!.IsSpeaking();
        }

        /// <inheritdoc />
        public string? ListVoicesJson()
        {
            if (!EnsureReady())
            {
                return null;
            }

            return bridge!.ListVoicesJson();
        }

        /// <inheritdoc />
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

        /// <summary>
        /// 拡張可能な Dispose パターン本体。
        /// </summary>
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
                    bridge?.Dispose();
                    bridge = null;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TextToSpeechAndroidService] Dispose 失敗: {e.Message}");
                }
            }
            disposed = true;
        }

        private bool EnsureReady()
        {
            if (disposed)
            {
                Debug.LogError("[TextToSpeechAndroidService] 既に Dispose 済みです。");
                return false;
            }
            if (bridge == null)
            {
                Debug.LogError("[TextToSpeechAndroidService] Init() がまだ呼ばれていません。");
                return false;
            }
            return true;
        }
    }
}
