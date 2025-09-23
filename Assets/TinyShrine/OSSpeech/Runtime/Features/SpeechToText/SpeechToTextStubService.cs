using System;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.SpeechToText
{
    /// <summary>
    /// 音声認識サービスのスタブ実装。
    /// プラットフォームでネイティブ音声認識が利用できない場合に使用されます。
    /// 主にEditor環境やテスト用途で使用します。
    /// </summary>
    public class SpeechToTextStubService : ISpeechToTextService
    {
        /// <summary>部分結果イベント（スタブでは発火しません）</summary>
        public event Action<string> OnPartial = (text) => { };

        /// <summary>確定結果イベント（スタブでは発火しません）</summary>
        public event Action<string> OnFinal = (text) => { };

        private bool initialized;
        private bool disposed;
        private bool isListening;

        /// <summary>
        /// スタブサービスを初期化します。
        /// </summary>
        /// <param name="mainContext">メインスレッドのSynchronizationContext</param>
        /// <param name="locale">認識言語（スタブでは無視されます）</param>
        public void Init(SynchronizationContext mainContext, string locale = "ja-JP")
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SpeechToTextStubService));
            }

            if (initialized)
            {
                Debug.LogWarning("[SpeechToTextStubService] Already initialized");
                return;
            }

            Debug.Log($"[SpeechToTextStubService] Initialized with locale: {locale} (Stub implementation)");
            initialized = true;
        }

        /// <summary>
        /// 音声認識を開始します（スタブでは模擬動作）。
        /// </summary>
        /// <returns>常にtrueを返します</returns>
        public bool Start()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SpeechToTextStubService));
            }

            if (!initialized)
            {
                Debug.LogWarning("[SpeechToTextStubService] Service not initialized");
                return false;
            }

            if (isListening)
            {
                Debug.LogWarning("[SpeechToTextStubService] Already listening");
                return true;
            }

            isListening = true;
            Debug.Log("[SpeechToTextStubService] Started listening (Stub - no actual recognition)");

            // スタブ実装では数秒後にダミーの結果を生成（オプション）
            if (Application.isPlaying)
            {
                SimulateRecognitionResult();
            }

            return true;
        }

        /// <summary>
        /// 音声認識を停止します。
        /// </summary>
        public void Stop()
        {
            if (disposed)
            {
                return;
            }

            if (!isListening)
            {
                Debug.LogWarning("[SpeechToTextStubService] Not currently listening");
                return;
            }

            isListening = false;
            Debug.Log("[SpeechToTextStubService] Stopped listening");
        }

        /// <summary>
        /// リソースを解放します。
        /// </summary>
        public void Dispose()
        {
            if (!disposed)
            {
                Stop();

                // イベントハンドラーをクリア
                OnPartial = (text) => { };
                OnFinal = (text) => { };

                disposed = true;
                Debug.Log("[SpeechToTextStubService] Disposed");
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// ダミーの音声認識結果をシミュレートします（開発・デバッグ用）。
        /// </summary>
        private async void SimulateRecognitionResult()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            try
            {
                // 2秒後に部分結果
                await System.Threading.Tasks.Task.Delay(2000);
                if (isListening && !disposed)
                {
                    OnPartial?.Invoke("こんにち");
                }

                // さらに1秒後に確定結果
                await System.Threading.Tasks.Task.Delay(1000);
                if (isListening && !disposed)
                {
                    OnFinal?.Invoke("こんにちは");
                    Stop();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SpeechToTextStubService] Error in simulation: {ex.Message}");
            }
        }
    }
}
