using System;
using System.Text;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.SpeechToText
{
    public class SpeechToTextAndroidService : ISpeechToTextService
    {
        private readonly StringBuilder textBuffer = new();
        private SpeechToTextAndroidBridge? androidBridge;
        private string currentLocale = "ja-JP";
        private bool isInitialized;
        private bool isListening;
        private bool isStopping;

        /// <summary>途中経過（部分結果）。UIに逐次表示したいときに。</summary>
        public event Action<string> OnPartial = text => { };

        /// <summary>確定結果。DB保存やLLM投入などはこちらで。</summary>
        public event Action<string> OnFinal = text => { };

        /// <summary>エラー発生時。エラーコードとメッセージを渡す。</summary>
        public event Action<int, string> OnError = (code, message) => { };

        /// <summary>音声認識の状態変化（準備完了、開始、終了等）</summary>
        public event Action<string> OnStateChanged = state => { };

        public bool IsListening => isListening;
        public bool IsInitialized => isInitialized;
        public string CurrentLocale => currentLocale;

        /// <summary>初期化（locale 例: "ja-JP"）</summary>
        public void Init(SynchronizationContext mainContext, string locale = "ja-JP")
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

                // Android Bridge を初期化
                androidBridge = new SpeechToTextAndroidBridge(mainContext);

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
                OnStateChanged?.Invoke("Initialized");

                Debug.Log($"[SpeechToTextAndroidService] Initialized with locale: {currentLocale}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidService] Initialization failed: {e.Message}");
                OnError?.Invoke(-1, $"Initialization failed: {e.Message}");
            }
        }

        public bool Start()
        {
            if (!isInitialized)
            {
                Debug.LogError("[SpeechToTextAndroidService] Not initialized. Call Init() first.");
                OnError?.Invoke(-1, "Service not initialized");
                return false;
            }

            if (isListening)
            {
                Debug.LogWarning("[SpeechToTextAndroidService] Already listening.");
                return true;
            }

            if (isStopping)
            {
                Debug.LogWarning("[SpeechToTextAndroidService] Currently stopping. Please wait.");
                return false;
            }

            textBuffer.Clear();
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
                OnError?.Invoke(-1, $"Failed to start: {e.Message}");
                return false;
            }
        }

        public void Stop()
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
                isStopping = true;
                isListening = false;
                androidBridge?.StopListening();
                Debug.Log("[SpeechToTextAndroidService] Stopped listening.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidService] Failed to stop listening: {e.Message}");
                OnError?.Invoke(-1, $"Failed to stop: {e.Message}");
            }
        }

        /// <summary>
        /// 音声認識をキャンセル
        /// </summary>
        public void Cancel()
        {
            if (!isInitialized || !isListening || isStopping)
            {
                return;
            }

            try
            {
                androidBridge?.Cancel();
                isListening = false;
                OnStateChanged?.Invoke("Cancelled");
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
        public void Dispose()
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

                OnStateChanged?.Invoke("Disposed");
                Debug.Log("[SpeechToTextAndroidService] Disposed.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidService] Error during disposal: {e.Message}");
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }

        // ---- Private Methods ----
        private void SetupEventHandlers()
        {
            if (androidBridge == null)
            {
                return;
            }

            androidBridge.OnReadyForSpeech += () => OnStateChanged?.Invoke("Ready");

            androidBridge.OnBeginningOfSpeech += () => OnStateChanged?.Invoke("Speaking");

            androidBridge.OnEndOfSpeech += () => OnStateChanged?.Invoke("ProcessingResults");

            androidBridge.OnError += (errorCode) =>
            {
                if (errorCode == SpeechToTextAndroidBridge.ErrorCode.NoMatch)
                {
                    if (androidBridge != null)
                    {
                        if (isListening)
                        {
                            Debug.Log("[SpeechToTextAndroidService] No match, restarting listening.");
                            androidBridge.StartListening();
                        }
                        else if (isStopping)
                        {
                            isListening = false;
                            isStopping = false;
                            Debug.Log("[SpeechToTextAndroidService] No match, stopping.");
                        }
                    }
                }
                else
                {
                    isListening = false;
                    string errorMessage = SpeechToTextAndroidBridge.GetErrorString(errorCode);
                    OnError?.Invoke((int)errorCode, errorMessage);
                    OnStateChanged?.Invoke("Error");
                    Debug.LogError(
                        $"[SpeechToTextAndroidService] Recognition error: {errorMessage} ({(int)errorCode})"
                    );
                }
            };

            androidBridge.OnResults += (resultText) =>
            {
                textBuffer.Append(resultText + " 。");
                var text = textBuffer.ToString();
                if (isListening)
                {
                    Debug.Log("[SpeechToTextAndroidService] Received results while still listening, ignoring.");
                    OnFinal?.Invoke(text);
                    OnStateChanged?.Invoke("ResultReceived");
                    Debug.Log($"[SpeechToTextAndroidService] Final result: {text}");
                    Debug.Log("[SpeechToTextAndroidService] No match, restarting listening.");
                    androidBridge.StartListening();
                }
                else if (isStopping)
                {
                    isStopping = false;
                    OnFinal?.Invoke(text);
                    OnStateChanged?.Invoke("StopAndResultReceived");
                    Debug.Log($"[SpeechToTextAndroidService] Recording is stopped, final result: {text}");
                }
                else
                {
                    Debug.LogWarning($"[SpeechToTextAndroidService] Should not receive final result: {text}");
                }
            };

            androidBridge.OnPartialResults += (partialText) =>
            {
                var textSoFar = textBuffer.ToString() ?? string.Empty;
                OnPartial?.Invoke(textSoFar + partialText);
                Debug.Log($"[SpeechToTextAndroidService] Partial result: {partialText}");
            };

            // 音声レベル変化は必要に応じて実装
            // OnVolumeChanged?.Invoke(rmsdB);
            androidBridge.OnRmsChanged += (rmsdB) => { };
        }
    }
}
