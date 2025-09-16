#if UNITY_IOS && !UNITY_EDITOR
#define STT_RUNTIME_IOS
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
#define STT_RUNTIME_MAC
#endif

using System;
#pragma warning disable IDE0005 // Using ディレクティブは必要ありません。
using System.Runtime.InteropServices;
#pragma warning restore IDE0005 // Using ディレクティブは必要ありません。
using System.Threading;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

namespace TinyShrine.OSSpeech.SpeechToText
{
    /// <summary>
    /// iOS/macOS の SFSpeechRecognizer を呼ぶ薄いサービス。
    /// MonoBehaviour に依存せず、イベントで結果を通知。
    /// </summary>
    public static class SpeechToTextService
    {
#if STT_RUNTIME_IOS
        const string LIB = "__Internal";
#elif STT_RUNTIME_MAC
        const string LIB = "SpeechToText"; // Plugins/macOS/SpeechToText.bundle（または .dylib）の実体名
#endif

        /// <summary>途中経過（部分結果）。UIに逐次表示したいときに。</summary>
        public static event Action<string> OnPartial = static text => { };

        /// <summary>確定結果。DB保存やLLM投入などはこちらで。</summary>
        public static event Action<string> OnFinal = static text => { };

#if STT_RUNTIME_IOS || STT_RUNTIME_MAC
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ResultCb([MarshalAs(UnmanagedType.LPUTF8Str)] string text, bool isFinal);

        [DllImport(LIB)]
        static extern void stt_set_callback(ResultCb cb);

        [DllImport(LIB)]
        static extern int stt_request_authorization(); // 3 = Authorized

        [DllImport(LIB)]
        static extern int stt_set_locale([MarshalAs(UnmanagedType.LPUTF8Str)] string locale);

        [DllImport(LIB)]
        static extern int stt_start();

        [DllImport(LIB)]
        static extern void stt_stop();

        private static string locale = "ja-JP";
        private static bool initialized;
        private static SynchronizationContext? mainCtx;
        private static ResultCb keep; // GC防止

        /// <summary>
        /// 初期化。メインスレッド（例: 任意の Start/Awake 内）で呼んで SynchronizationContext を捕まえるのが安全。
        /// </summary>
        public static void Init(string locale = "ja-JP", SynchronizationContext? mainContext = null)
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            SpeechToTextService.locale = string.IsNullOrEmpty(locale) ? "ja-JP" : locale;
            mainCtx = mainContext ?? SynchronizationContext.Current; // 取得できない環境でも動くようにフォールバック
            keep = OnNativeResult; // デリゲートを静的保持（AOT/GC対策）
            stt_set_callback(keep);

            var auth = stt_request_authorization(); // 3=Authorized
            if (auth != 3)
                UnityEngine.Debug.LogWarning($"[SpeechToTextService] Speech auth status: {auth}");

            stt_set_locale(locale);
            UnityEngine.Debug.LogWarning("[SpeechToTextService] iOS/macOS 実機ビルドで有効になります。");
        }

        public static void SetLocale(string locale)
        {
            SpeechToTextService.locale = string.IsNullOrEmpty(locale) ? "ja-JP" : locale;
            var rc = stt_set_locale(locale);
            if (rc != 0)
                UnityEngine.Debug.LogWarning($"[SpeechToTextService] set_locale failed: {rc}（実行中は変更不可）");
        }

        public static bool Start()
        {
            var rc = stt_start();
            if (rc != 0)
            {
                UnityEngine.Debug.LogError($"[SpeechToTextService] stt_start failed: {rc}");
                return false;
            }
            return true;
        }

        public static void Stop()
        {
            stt_stop();
        }

        [AOT.MonoPInvokeCallback(typeof(ResultCb))]
        static void OnNativeResult(string text, bool isFinal)
        {
            // Unity APIはメインスレッドのみ安全。必要ならメインへディスパッチ。
            void Raise()
            {
                if (isFinal)
                {
                    OnFinal?.Invoke(text);
                }
                else
                {
                    OnPartial?.Invoke(text);
                }
            }

            var ctx = mainCtx;
            if (ctx != null)
            {
                ctx.Post(_ => Raise(), null);
            }
            else
            {
                Raise(); // 最悪フォールバック（自分でメインスレッド制御している場合を想定）
            }
        }
#elif UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaClass bridge; // jp.tinyshrine.osspeech.SpeechToTextBridge
        static AndroidJavaObject activity; // UnityのcurrentActivity
        static SynchronizationContext ctx;

        /// <summary>初期化（locale 例: "ja-JP"）</summary>
        public static void Init(string locale = "ja-JP", SynchronizationContext? mainContext = null)
        {
            ctx = mainContext ?? SynchronizationContext.Current;

            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            bridge = new AndroidJavaClass("jp.tinyshrine.osspeech.SpeechToTextBridge");
            bridge.CallStatic("init", activity, new CallbackProxy());

            // ロケール適用
            SetLocale(locale);

            // 端末に認識エンジンが無いケースに備えてログ（必要なら呼び元でチェックしてください）
            bool available = bridge.CallStatic<bool>("isRecognitionAvailable", activity);
            if (!available)
                Debug.LogWarning("[STT] SpeechRecognizer is not available on this device.");
        }

        /// <summary>ロケールを設定（例: "ja-JP"）</summary>
        public static void SetLocale(string locale)
        {
            if (bridge == null)
                return;
            bridge.CallStatic("setLanguage", string.IsNullOrEmpty(locale) ? "ja-JP" : locale);
        }

        /// <summary>認識開始。true=開始処理を投げられた/false=初期化されてない等</summary>
        public static bool Start()
        {
            const string Mic = "android.permission.RECORD_AUDIO";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(Mic))
            {
                var cb = new UnityEngine.Android.PermissionCallbacks();

                cb.PermissionGranted += _ =>
                {
                    Debug.Log("[STT] Mic permission granted. Starting…");
                    try
                    {
                        bridge?.CallStatic("start");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                };

                cb.PermissionDenied += _ =>
                {
                    bool dontAskAgain = !UnityEngine.Android.Permission.ShouldShowRequestPermissionRationale(Mic);
                    if (dontAskAgain)
                    {
                        Debug.LogError("[STT] Microphone permission denied with 'Don't ask again'. Opening settings…");
                        OpenAppSettings();
                    }
                    else
                    {
                        Debug.LogError("[STT] Microphone permission denied.");
                    }
                };

                UnityEngine.Android.Permission.RequestUserPermission(Mic, cb);
                return false;
            }

            if (bridge != null && bridge.CallStatic<bool>("isListening"))
            {
                Debug.LogWarning("[STT] Already listening.");
                return false;
            }

            try
            {
                bridge?.CallStatic("start");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[STT] start failed: " + e.Message);
                return false;
            }
        }

        public static void OpenAppSettings()
        {
            try
            {
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                activity.Call(
                    "runOnUiThread",
                    new AndroidJavaRunnable(() =>
                    {
                        // action: Settings.ACTION_APPLICATION_DETAILS_SETTINGS
                        using var settings = new AndroidJavaClass("android.provider.Settings");
                        string action = settings.GetStatic<string>("ACTION_APPLICATION_DETAILS_SETTINGS");

                        // intent = new Intent(action)
                        using var intent = new AndroidJavaObject("android.content.Intent", action);

                        // uri = Uri.fromParts("package", <packageName>, null)
                        using var uriClass = new AndroidJavaClass("android.net.Uri");
                        using var uri = uriClass.CallStatic<AndroidJavaObject>(
                            "fromParts",
                            "package",
                            Application.identifier,
                            null
                        );

                        // intent.setData(uri).addFlags(FLAG_ACTIVITY_NEW_TASK)
                        intent.Call<AndroidJavaObject>("setData", uri);

                        using var intentClass = new AndroidJavaClass("android.content.Intent");
                        int FLAG_NEW_TASK = intentClass.GetStatic<int>("FLAG_ACTIVITY_NEW_TASK");
                        intent.Call<AndroidJavaObject>("addFlags", FLAG_NEW_TASK);

                        // startActivity(intent)
                        activity.Call("startActivity", intent);
                    })
                );
            }
            catch (Exception e)
            {
                Debug.LogError("[STT] OpenAppSettings failed: " + e);
            }
        }

        /// <summary>認識停止（最終結果が来る想定）</summary>
        public static void Stop()
        {
            if (bridge == null)
            {
                return;
            }
            try
            {
                bridge.CallStatic("stop");
            }
            catch (Exception e)
            {
                Debug.LogError("[STT] stop failed: " + e.Message);
            }
        }

        // ───────── internal ─────────

        // Java側コールバックを受けるプロキシ
        class CallbackProxy : AndroidJavaProxy
        {
            public CallbackProxy()
                : base("jp.tinyshrine.osspeech.SpeechToTextBridge$Callback") { }

            void Post(Action a)
            {
                if (ctx != null)
                {
                    ctx.Post(_ => a(), null);
                }
                else
                {
                    a();
                }
            }

            // Java: onPartial(String)
            public void onPartial(string text) => Post(() => OnPartial(text ?? string.Empty));

            // Java: onFinal(String)
            public void onFinal(string text) => Post(() => OnFinal(text ?? string.Empty));

            // Java: onReady/onBegin/onEnd/onError はログだけ（iOS側APIに無いのでイベントには出さない）
            public void onReady() => Debug.Log("[STT] Ready");

            public void onBegin() => Debug.Log("[STT] Begin");

            public void onEnd() => Debug.Log("[STT] End");

            public void onError(int code, string message) => Debug.LogError($"[STT] Error {code}: {message}");
        }
#else
        // エディタや非対応環境ではダミー実装
        public static void Init(string locale = "ja-JP", SynchronizationContext? mainContext = null)
        {
            UnityEngine.Debug.LogWarning("[SpeechToTextService] iOS/macOS 以外の環境では無効です。");
        }

        public static void SetLocale(string locale)
        {
            UnityEngine.Debug.LogWarning("[SpeechToTextService] iOS/macOS 以外の環境では無効です。");
        }

        public static bool Start()
        {
            UnityEngine.Debug.LogWarning("[SpeechToTextService] iOS/macOS 以外の環境では無効です。");
            return false;
        }

        public static void Stop()
        {
            UnityEngine.Debug.LogWarning("[SpeechToTextService] iOS/macOS 以外の環境では無効です。");
        }
#endif
    }
}
