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
