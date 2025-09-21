#if UNITY_ANDROID || UNITY_EDITOR

using System;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.SpeechToText
{
    /// <summary>
    /// Android SpeechRecognizer API をラップする AndroidJavaProxy
    /// SpeechToTextBridge.java の Callback インターフェースに対応
    /// </summary>
    public sealed class SpeechToTextAndroidBridge : AndroidJavaProxy, IDisposable
    {
        // Android SpeechRecognizer エラーコード定数
        public const int ErrorNetworkTimeout = 1;
        public const int ErrorNetwork = 2;
        public const int ErrorAudio = 3;
        public const int ErrorServer = 4;
        public const int ErrorClient = 5;
        public const int ErrorSpeechTimeout = 6;
        public const int ErrorNoMatch = 7;
        public const int ErrorRecognizerBusy = 8;
        public const int ErrorInsufficientPermissions = 9;

        private const string AndroidBridgeClass = "jp.tinyshrine.osspeech.SpeechToTextBridge";
        private const string CallbackInterface = "jp.tinyshrine.osspeech.SpeechToTextBridge$Callback";

        private readonly AndroidJavaObject unityActivity;
        private readonly AndroidJavaClass bridgeClass;
        private readonly SynchronizationContext? mainContext;
        private bool disposed;

        public SpeechToTextAndroidBridge(SynchronizationContext? mainContext = null)
            : base(CallbackInterface)
        {
            this.mainContext = mainContext ?? SynchronizationContext.Current;

            // Unity Activity を取得
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            // Bridge クラスを取得
            bridgeClass = new AndroidJavaClass(AndroidBridgeClass);

            // Java 側を初期化
            bridgeClass.CallStatic("init", unityActivity, this);
        }

        ~SpeechToTextAndroidBridge()
        {
            Dispose(false);
        }

        // イベント
        public event Action OnReadyForSpeech = () => { };
        public event Action OnBeginningOfSpeech = () => { };
        public event Action<float> OnRmsChanged = rmsdB => { };
        public event Action OnEndOfSpeech = () => { };
        public event Action<int> OnError = error => { };
        public event Action<string> OnResults = text => { };
        public event Action<string> OnPartialResults = text => { };

        // ---- Public API ----

        /// <summary>
        /// エラーコードを文字列に変換
        /// </summary>
        public static string GetErrorString(int errorCode)
        {
            return errorCode switch
            {
                ErrorNetworkTimeout => "Network timeout",
                ErrorNetwork => "Network error",
                ErrorAudio => "Audio recording error",
                ErrorServer => "Server error",
                ErrorClient => "Client error",
                ErrorSpeechTimeout => "Speech input timeout",
                ErrorNoMatch => "No speech match",
                ErrorRecognizerBusy => "Recognition service busy",
                ErrorInsufficientPermissions => "Insufficient permissions",
                _ => $"Unknown error ({errorCode})",
            };
        }

        /// <summary>
        /// 音声認識が利用可能かチェック
        /// </summary>
        public bool IsRecognitionAvailable()
        {
            return bridgeClass.CallStatic<bool>("isRecognitionAvailable", unityActivity);
        }

        /// <summary>
        /// 言語設定（例: "ja-JP", "en-US"）
        /// </summary>
        public void SetLanguage(string languageTag)
        {
            bridgeClass.CallStatic("setLanguage", languageTag);
        }

        /// <summary>
        /// オフライン優先設定
        /// </summary>
        public void SetPreferOffline(bool preferOffline)
        {
            bridgeClass.CallStatic("setPreferOffline", preferOffline);
        }

        /// <summary>
        /// 部分結果取得設定
        /// </summary>
        public void SetPartialResults(bool enable)
        {
            bridgeClass.CallStatic("setPartialResults", enable);
        }

        /// <summary>
        /// 音声認識開始
        /// </summary>
        public void StartListening()
        {
            bridgeClass.CallStatic("startListening");
        }

        /// <summary>
        /// 音声認識停止
        /// </summary>
        public void StopListening()
        {
            bridgeClass.CallStatic("stopListening");
        }

        /// <summary>
        /// 音声認識キャンセル
        /// </summary>
        public void Cancel()
        {
            bridgeClass.CallStatic("cancel");
        }

        /// <summary>
        /// Bundle から全ての認識結果を取得
        /// </summary>
        public string[] GetAllResults(AndroidJavaObject results)
        {
            if (results == null)
            {
                return Array.Empty<string>();
            }

            try
            {
                AndroidJavaObject arrayList = bridgeClass.CallStatic<AndroidJavaObject>("getAllResults", results);
                if (arrayList == null)
                {
                    return Array.Empty<string>();
                }

                int size = arrayList.Call<int>("size");
                string[] resultArray = new string[size];

                for (int i = 0; i < size; i++)
                {
                    string item = arrayList.Call<string>("get", i);
                    resultArray[i] = item ?? string.Empty;
                }

                return resultArray;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidBridge] Failed to get all results: {e.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// リソース解放
        /// </summary>
        public void Destroy()
        {
            bridgeClass?.CallStatic("destroy");
        }

#pragma warning disable IDE1006, SA1300 // 命名スタイル
        // ---- AndroidJavaProxy Callbacks ----
        // Java側のCallbackインターフェースメソッドに対応

        /// <summary>
        /// 音声認識準備完了時
        /// </summary>
        public void onReadyForSpeech(AndroidJavaObject parameters)
        {
            InvokeOnMainThread(() => OnReadyForSpeech?.Invoke());
        }

        /// <summary>
        /// 音声入力開始時
        /// </summary>
        public void onBeginningOfSpeech()
        {
            InvokeOnMainThread(() => OnBeginningOfSpeech?.Invoke());
        }

        /// <summary>
        /// 音声レベル変化時
        /// </summary>
        public void onRmsChanged(float rmsdB)
        {
            InvokeOnMainThread(() => OnRmsChanged?.Invoke(rmsdB));
        }

        /// <summary>
        /// 音声バッファ受信時（通常使用しない）
        /// </summary>
        public void onBufferReceived(AndroidJavaObject buffer)
        {
            // 通常は使用しない
        }

        /// <summary>
        /// 音声入力終了時
        /// </summary>
        public void onEndOfSpeech()
        {
            InvokeOnMainThread(() => OnEndOfSpeech?.Invoke());
        }

        /// <summary>
        /// エラー発生時
        /// </summary>
        public void onError(int error)
        {
            InvokeOnMainThread(() => OnError?.Invoke(error));
        }

        /// <summary>
        /// 最終認識結果受信時
        /// </summary>
        public void onResults(AndroidJavaObject results)
        {
            string resultText = GetFirstResult(results);
            InvokeOnMainThread(() => OnResults?.Invoke(resultText));
        }

        /// <summary>
        /// 部分認識結果受信時
        /// </summary>
        public void onPartialResults(AndroidJavaObject partialResults)
        {
            string resultText = GetFirstResult(partialResults);
            InvokeOnMainThread(() => OnPartialResults?.Invoke(resultText));
        }

        /// <summary>
        /// カスタムイベント受信時（通常使用しない）
        /// </summary>
        public void onEvent(int eventType, AndroidJavaObject parameters)
        {
            // 通常は使用しない
        }
#pragma warning restore IDE1006, SA1300 // 命名スタイル

        // ---- IDisposable Support ----
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // ---- Utility Methods ----
        private void InvokeOnMainThread(Action action)
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

        /// <summary>
        /// Bundle から最初の認識結果を取得
        /// </summary>
        private string GetFirstResult(AndroidJavaObject results)
        {
            if (results == null)
            {
                return string.Empty;
            }

            try
            {
                return bridgeClass.CallStatic<string>("getFirstResult", results);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidBridge] Failed to get result: {e.Message}");
                return string.Empty;
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                try
                {
                    Destroy();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SpeechToTextAndroidBridge] Error during disposal: {e.Message}");
                }

                bridgeClass.Dispose();
                disposed = true;
            }
        }
    }
}

#endif
