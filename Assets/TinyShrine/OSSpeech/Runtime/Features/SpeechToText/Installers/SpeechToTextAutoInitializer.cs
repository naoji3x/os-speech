using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

namespace TinyShrine.OSSpeech.SpeechToText.Installers
{
    /// <summary>
    /// 音声認識サービスの自動初期化を行うクラス。
    /// アプリケーション開始時にISpeechToTextServiceを自動的に初期化します。
    /// </summary>
    public class SpeechToTextAutoInitializer : IStartable, IDisposable
    {
        private readonly ISpeechToTextService speechToTextService;
        private bool initialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpeechToTextAutoInitializer"/> class.
        /// </summary>
        /// <param name="speechService">注入される音声認識サービス</param>
        public SpeechToTextAutoInitializer(ISpeechToTextService speechService)
        {
            this.speechToTextService = speechService;
        }

        /// <summary>
        /// VContainerのStartableとして、アプリケーション開始時に呼び出されます。
        /// 音声認識サービスを初期化し、基本設定を行います。
        /// </summary>
        public void Start()
        {
            if (initialized)
            {
                return;
            }

            try
            {
                // メインスレッドのSynchronizationContextを取得
                var mainContext = SynchronizationContext.Current;

                // 音声認識サービスを初期化（日本語設定）
                speechToTextService.Init(mainContext, "ja-JP");

                initialized = true;
                Debug.Log("[SpeechToTextAutoInitializer] Speech recognition service initialized successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError(
                    $"[SpeechToTextAutoInitializer] Failed to initialize speech recognition service: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// アプリケーション終了時のクリーンアップを行います。
        /// </summary>
        public void Dispose()
        {
            if (initialized && speechToTextService != null)
            {
                try
                {
                    speechToTextService.Stop();
                    Debug.Log("[SpeechToTextAutoInitializer] Speech recognition service disposed");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[SpeechToTextAutoInitializer] Error during disposal: {ex.Message}");
                }
                finally
                {
                    initialized = false;
                }
            }
            GC.SuppressFinalize(this);
        }
    }
}
