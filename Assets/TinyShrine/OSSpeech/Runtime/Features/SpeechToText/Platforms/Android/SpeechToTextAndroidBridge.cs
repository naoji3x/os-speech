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
        // ---- Android SpeechRecognizer エラーコード ----
        public enum ErrorCode
        {
            NetworkTimeout = 1,
            Network = 2,
            Audio = 3,
            Server = 4,
            Client = 5,
            SpeechTimeout = 6,
            NoMatch = 7,
            RecognizerBusy = 8,
            InsufficientPermissions = 9,
        }

        // ---- Java 側シンボル ----
        private const string AndroidBridgeClass = "jp.tinyshrine.osspeech.SpeechToTextBridge";
        private const string CallbackInterface = "jp.tinyshrine.osspeech.SpeechToTextBridge$Callback";

        // メインスレッド呼び出しの割当て削減（毎回のラムダ生成を回避）
        private static readonly SendOrPostCallback PostInvoker = state =>
        {
            try
            {
                ((Action)state!).Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        };

        // ---- フィールド ----
        private readonly AndroidJavaClass bridgeClass;
        private readonly SynchronizationContext mainContext;
        private AndroidJavaObject? unityActivity;

        // Dispose の原子化（0=alive, 1=disposed）
        private int disposed;
        private bool IsDisposed => Volatile.Read(ref disposed) == 1;

        private bool TryBeginDispose() => Interlocked.Exchange(ref disposed, 1) == 0;

        // ---- ctor ----
        public SpeechToTextAndroidBridge(SynchronizationContext mainContext, string locale = "ja-JP")
            : base(CallbackInterface)
        {
            // メインスレッドの SynchronizationContext を必須化（nullなら例外）
            this.mainContext = mainContext ?? throw new ArgumentNullException(nameof(mainContext));

            // Unity Activity を取得
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            // Bridge クラスを取得
            bridgeClass = new AndroidJavaClass(AndroidBridgeClass);

            // Java 側を初期化
            SafeCallStatic("init", unityActivity, this);

            // 言語設定
            SetLanguage(locale);
        }

        // ---- イベント（null 許容）----
        public event Action? OnReadyForSpeech;
        public event Action? OnBeginningOfSpeech;
        public event Action<float>? OnRmsChanged;
        public event Action? OnEndOfSpeech;
        public event Action<ErrorCode>? OnError;
        public event Action<string>? OnResults;
        public event Action<string>? OnPartialResults;

        // ---- Public API ----

        /// <summary>エラーコードを文字列に変換</summary>
        public static string GetErrorString(ErrorCode errorCode) =>
            errorCode switch
            {
                ErrorCode.NetworkTimeout => "Network timeout",
                ErrorCode.Network => "Network error",
                ErrorCode.Audio => "Audio recording error",
                ErrorCode.Server => "Server error",
                ErrorCode.Client => "Client error",
                ErrorCode.SpeechTimeout => "Speech input timeout",
                ErrorCode.NoMatch => "No speech match",
                ErrorCode.RecognizerBusy => "Recognition service busy",
                ErrorCode.InsufficientPermissions => "Insufficient permissions",
                _ => $"Unknown error ({errorCode})",
            };

        /// <summary>音声認識が利用可能か</summary>
        public bool IsRecognitionAvailable()
        {
            ThrowIfDisposed();
            var act = GetActivityOrNull();
            if (act == null)
            {
                Debug.LogError("[SpeechToTextAndroidBridge] currentActivity is null.");
                return false;
            }
            return SafeCallStaticRet<bool, AndroidJavaObject?>("isRecognitionAvailable", act);
        }

        /// <summary>言語設定（例: "ja-JP", "en-US"）</summary>
        public void SetLanguage(string languageTag)
        {
            ThrowIfDisposed();
            SafeCallStatic("setLanguage", languageTag);
        }

        /// <summary>オフライン優先設定</summary>
        public void SetPreferOffline(bool preferOffline)
        {
            ThrowIfDisposed();
            SafeCallStatic("setPreferOffline", preferOffline);
        }

        /// <summary>部分結果取得設定</summary>
        public void SetPartialResults(bool enable)
        {
            ThrowIfDisposed();
            SafeCallStatic("setPartialResults", enable);
        }

        /// <summary>音声認識開始</summary>
        public void StartListening()
        {
            ThrowIfDisposed();
            SafeCallStatic("startListening");
        }

        /// <summary>音声認識停止</summary>
        public void StopListening()
        {
            ThrowIfDisposed();
            SafeCallStatic("stopListening");
        }

        /// <summary>音声認識キャンセル</summary>
        public void Cancel()
        {
            ThrowIfDisposed();
            SafeCallStatic("cancel");
        }

        /// <summary>リソース解放（Java 側の登録解除）</summary>
        public void Destroy()
        {
            // no-op 安全化：Dispose 済みでも例外にしない
            if (IsDisposed)
            {
                return;
            }
            DestroyInternal();
        }

        // ---- AndroidJavaProxy Callbacks ----
#pragma warning disable IDE1006, SA1300
        public void onReadyForSpeech(AndroidJavaObject parameters)
        {
            if (IsDisposed)
            {
                return;
            }

            InvokeOnMainThread(() => OnReadyForSpeech?.Invoke());
        }

        public void onBeginningOfSpeech()
        {
            if (IsDisposed)
            {
                return;
            }

            InvokeOnMainThread(() => OnBeginningOfSpeech?.Invoke());
        }

        public void onRmsChanged(float rmsdB)
        {
            if (IsDisposed)
            {
                return;
            }

            InvokeOnMainThread(() => OnRmsChanged?.Invoke(rmsdB));
        }

        public void onBufferReceived(AndroidJavaObject buffer)
        {
            // 通常は使用しない
        }

        public void onEndOfSpeech()
        {
            if (IsDisposed)
            {
                return;
            }

            InvokeOnMainThread(() => OnEndOfSpeech?.Invoke());
        }

        public void onError(int error)
        {
            if (IsDisposed)
            {
                return;
            }

            var code = (ErrorCode)error;
            InvokeOnMainThread(() => OnError?.Invoke(code));
        }

        public void onResults(AndroidJavaObject results)
        {
            if (IsDisposed)
            {
                return;
            }

            string resultText = GetFirstResult(results);
            InvokeOnMainThread(() => OnResults?.Invoke(resultText));
        }

        public void onPartialResults(AndroidJavaObject partialResults)
        {
            if (IsDisposed)
            {
                return;
            }

            string resultText = GetFirstResult(partialResults);
            InvokeOnMainThread(() => OnPartialResults?.Invoke(resultText));
        }

        public void onEvent(int eventType, AndroidJavaObject parameters)
        {
            // 通常は使用しない
        }
#pragma warning restore IDE1006, SA1300

        // ---- IDisposable ----
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!TryBeginDispose())
            {
                return;
            }

            try
            {
                if (disposing)
                {
                    // Java 側の登録解除
                    DestroyInternal();

                    // AndroidJavaObject の解放
                    try
                    {
                        bridgeClass?.Dispose();
                    }
                    catch
                    { /* ignore */
                    }
                    try
                    {
                        unityActivity?.Dispose();
                    }
                    catch
                    { /* ignore */
                    }
                    unityActivity = null;

                    // イベント購読を解除（GC支援）
                    OnReadyForSpeech = null;
                    OnBeginningOfSpeech = null;
                    OnRmsChanged = null;
                    OnEndOfSpeech = null;
                    OnError = null;
                    OnResults = null;
                    OnPartialResults = null;
                }
                // disposing==false（finalizer）は存在しないのでここは来ない
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidBridge] Dispose error: {e.Message}");
            }
        }

        // ---- Utility ----
        private void InvokeOnMainThread(Action action)
        {
            // 必ずメインスレッドへポスト（例外は PostInvoker 内でログ）
            mainContext.Post(PostInvoker, action);
        }

        /// <summary>Bundle から最初の認識結果を取得</summary>
        private string GetFirstResult(AndroidJavaObject results)
        {
            if (IsDisposed || results == null)
            {
                return string.Empty;
            }

            try
            {
                return bridgeClass.CallStatic<string>("getFirstResult", results) ?? string.Empty;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidBridge] Failed to get result: {e.Message}");
                return string.Empty;
            }
        }

        /// <summary>Java 側 destroy（例外で停止しない）</summary>
        private void DestroyInternal()
        {
            SafeCallStatic("destroy");
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(SpeechToTextAndroidBridge));
            }
        }

        /// <summary>Unity の currentActivity を必要に応じて取り直し</summary>
        private AndroidJavaObject? GetActivityOrNull()
        {
            if (IsDisposed)
            {
                return null;
            }

            if (unityActivity != null)
            {
                return unityActivity;
            }

            try
            {
                using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                return unityActivity = up.GetStatic<AndroidJavaObject>("currentActivity");
            }
            catch
            {
                return null;
            }
        }

        // ---- Java 呼び出しの安全ラッパ ----
        private void SafeCallStatic(string method)
        {
            try
            {
                bridgeClass.CallStatic(method);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidBridge] {method} failed: {e.Message}");
            }
        }

        private void SafeCallStatic<T1>(string method, T1 arg1)
        {
            try
            {
                bridgeClass.CallStatic(method, arg1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidBridge] {method} failed: {e.Message}");
            }
        }

        private void SafeCallStatic<T1, T2>(string method, T1 arg1, T2 arg2)
        {
            try
            {
                bridgeClass.CallStatic(method, arg1, arg2);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidBridge] {method} failed: {e.Message}");
            }
        }

        private T SafeCallStaticRet<T>(string method)
        {
            try
            {
                return bridgeClass.CallStatic<T>(method);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidBridge] {method} failed: {e.Message}");
                return default!;
            }
        }

        private T SafeCallStaticRet<T, T1>(string method, T1 arg1)
        {
            try
            {
                return bridgeClass.CallStatic<T>(method, arg1);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SpeechToTextAndroidBridge] {method} failed: {e.Message}");
                return default!;
            }
        }
    }
}
