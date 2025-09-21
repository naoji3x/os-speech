using System;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.SpeechToText
{
    public static class SpeechToTextAndroidService
    {
        private static SpeechToTextAndroidBridge? androidBridge;
        private static SynchronizationContext? mainContext;
        private static string currentLocale = "ja-JP";
        private static bool isInitialized;
        private static bool isListening;

        /// <summary>途中経過（部分結果）。UIに逐次表示したいときに。</summary>
        public static event Action<string> OnPartial = static text => { };

        /// <summary>確定結果。DB保存やLLM投入などはこちらで。</summary>
        public static event Action<string> OnFinal = static text => { };

        /// <summary>エラー発生時。エラーコードとメッセージを渡す。</summary>
        public static event Action<int, string> OnError = static (code, message) => { };

        /// <summary>音声認識の状態変化（準備完了、開始、終了等）</summary>
        public static event Action<string> OnStateChanged = static state => { };

        public static bool IsListening => isListening;
        public static bool IsInitialized => isInitialized;
        public static string CurrentLocale => currentLocale;

        /// <summary>初期化（locale 例: "ja-JP"）</summary>
        public static void Init(string locale = "ja-JP", SynchronizationContext? mainContext = null)
        {
            if (isInitialized)
            {
                Debug.LogWarning(
                    "[SpeechToTextAndroidService] Already initialized. Call Dispose() first if you want to reinitialize."
                );
                return;
            }

            try
            {
                currentLocale = locale;
                SpeechToTextAndroidService.mainContext = mainContext ?? SynchronizationContext.Current;

                // Android Bridge を初期化
                androidBridge = new SpeechToTextAndroidBridge();

                // 音声認識が利用可能かチェック
                if (!androidBridge.IsRecognitionAvailable())
                {
                    Debug.LogError("[SpeechToTextAndroidService] Speech recognition is not available on this device.");
                    OnError?.Invoke(-1, "Speech recognition not available");
                    return;
                }

                // イベントハンドラーを設定
                SetupEventHandlers();

                // Android Bridge の設定
                androidBridge.SetLanguage(currentLocale);
                androidBridge.SetPartialResults(true);
                androidBridge.SetPreferOffline(false);

                isInitialized = true;
                InvokeOnMainThread(() => OnStateChanged?.Invoke("Initialized"));

                Debug.Log($"[SpeechToTextAndroidService] Initialized with locale: {currentLocale}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidService] Initialization failed: {e.Message}");
                InvokeOnMainThread(() => OnError?.Invoke(-1, $"Initialization failed: {e.Message}"));
            }
        }

        public static bool Start()
        {
            if (!isInitialized)
            {
                Debug.LogError("[SpeechToTextAndroidService] Not initialized. Call Init() first.");
                InvokeOnMainThread(() => OnError?.Invoke(-1, "Service not initialized"));
                return false;
            }

            if (isListening)
            {
                Debug.LogWarning("[SpeechToTextAndroidService] Already listening.");
                return true;
            }

            try
            {
                androidBridge?.StartListening();
                isListening = true;
                Debug.Log("[SpeechToTextAndroidService] Started listening.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidService] Failed to start listening: {e.Message}");
                InvokeOnMainThread(() => OnError?.Invoke(-1, $"Failed to start: {e.Message}"));
                return false;
            }
        }

        public static void Stop()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[SpeechToTextAndroidService] Not initialized.");
                return;
            }

            if (!isListening)
            {
                Debug.LogWarning("[SpeechToTextAndroidService] Not currently listening.");
                return;
            }

            try
            {
                androidBridge?.StopListening();
                isListening = false;
                Debug.Log("[SpeechToTextAndroidService] Stopped listening.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidService] Failed to stop listening: {e.Message}");
                InvokeOnMainThread(() => OnError?.Invoke(-1, $"Failed to stop: {e.Message}"));
            }
        }

        /// <summary>
        /// 音声認識をキャンセル
        /// </summary>
        public static void Cancel()
        {
            if (!isInitialized || !isListening)
            {
                return;
            }

            try
            {
                androidBridge?.Cancel();
                isListening = false;
                InvokeOnMainThread(() => OnStateChanged?.Invoke("Cancelled"));
                Debug.Log("[SpeechToTextAndroidService] Cancelled listening.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidService] Failed to cancel: {e.Message}");
            }
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public static void Dispose()
        {
            try
            {
                if (isListening)
                {
                    Stop();
                }

                androidBridge?.Dispose();
                androidBridge = null;

                isInitialized = false;
                isListening = false;

                InvokeOnMainThread(() => OnStateChanged?.Invoke("Disposed"));
                Debug.Log("[SpeechToTextAndroidService] Disposed.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidService] Error during disposal: {e.Message}");
            }
        }

        // ---- Private Methods ----
        private static void SetupEventHandlers()
        {
            if (androidBridge == null)
            {
                return;
            }

            androidBridge.OnReadyForSpeech += () => InvokeOnMainThread(() => OnStateChanged?.Invoke("Ready"));

            androidBridge.OnBeginningOfSpeech += () => InvokeOnMainThread(() => OnStateChanged?.Invoke("Speaking"));

            androidBridge.OnEndOfSpeech += () => InvokeOnMainThread(() => OnStateChanged?.Invoke("ProcessingResults"));

            androidBridge.OnError += (errorCode) =>
            {
                isListening = false;
                string errorMessage = SpeechToTextAndroidBridge.GetErrorString(errorCode);
                InvokeOnMainThread(() =>
                {
                    OnError?.Invoke(errorCode, errorMessage);
                    OnStateChanged?.Invoke("Error");
                });
                Debug.LogError($"[SpeechToTextAndroidService] Recognition error: {errorMessage} ({errorCode})");
            };

            androidBridge.OnResults += (resultText) =>
            {
                isListening = false;
                InvokeOnMainThread(() =>
                {
                    OnFinal?.Invoke(resultText);
                    OnStateChanged?.Invoke("ResultReceived");
                });
                Debug.Log($"[SpeechToTextAndroidService] Final result: {resultText}");
            };

            androidBridge.OnPartialResults += (partialText) =>
            {
                InvokeOnMainThread(() => OnPartial?.Invoke(partialText));
                Debug.Log($"[SpeechToTextAndroidService] Partial result: {partialText}");
            };

            // 音声レベル変化は必要に応じて実装
            // InvokeOnMainThread(() => OnVolumeChanged?.Invoke(rmsdB));
            androidBridge.OnRmsChanged += (rmsdB) => { };
        }

        private static void InvokeOnMainThread(Action action)
        {
            if (mainContext != null)
            {
                mainContext.Post(_ => action(), null);
            }
            else
            {
                action();
            }
        }
    }
}
