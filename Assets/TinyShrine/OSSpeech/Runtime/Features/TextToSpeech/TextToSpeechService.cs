#if UNITY_IOS && !UNITY_EDITOR
#define STT_RUNTIME_IOS
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
#define STT_RUNTIME_MAC
#endif

using System;
#pragma warning disable IDE0005 // Using ディレクティブは必要ありません。
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
#pragma warning restore IDE0005 // Using ディレクティブは必要ありません。

namespace TinyShrine.OSSpeech.TextToSpeech
{
    public static class TextToSpeechService
    {
#if STT_RUNTIME_IOS
        private const string LIB = "__Internal";
#elif STT_RUNTIME_MAC
        private const string LIB = "TextToSpeech"; // Plugins/macOS/TextToSpeech.bundle（または .dylib）の実体名
#endif

        public static event Action OnStart = static () => { };
        public static event Action OnFinish = static () => { };
        public static event Action OnCancel = static () => { };
        public static event Action OnError = static () => { };

#if STT_RUNTIME_IOS || STT_RUNTIME_MAC
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void EventCb(int ev); // 0=Start,1=Finish,2=Cancel,5=Error

        [DllImport(LIB)]
        private static extern void tts_set_event_callback(EventCb cb);

        [DllImport(LIB)]
        private static extern int tts_set_language([MarshalAs(UnmanagedType.LPUTF8Str)] string lang);

        [DllImport(LIB)]
        private static extern int tts_set_voice_id([MarshalAs(UnmanagedType.LPUTF8Str)] string id);

        [DllImport(LIB)]
        private static extern int tts_is_speaking();

        [DllImport(LIB)]
        private static extern void tts_stop();

        [DllImport(LIB)]
        private static extern int tts_speak(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            IntPtr voiceOrNull,
            float rate01,
            float pitch,
            float volume01,
            bool queue
        );

        [DllImport(LIB)]
        private static extern int tts_synthesize_pcm(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            IntPtr voiceOrNull,
            float rate01,
            float pitch,
            float volume01,
            out IntPtr outSamples,
            out int outFrameCount,
            out int outSampleRate
        );

        [DllImport(LIB)]
        private static extern void tts_free(IntPtr p);

        [DllImport(LIB)]
        private static extern IntPtr tts_list_voices_json();

        static SynchronizationContext _main;
        static EventCb _keep; // GC対策（AOT）

        /// <summary>メインスレッドで呼ぶこと。SynchronizationContext を捕まえます。</summary>
        public static void Init(SynchronizationContext? mainContext = null, string language = "ja-JP")
        {
            _main = mainContext ?? SynchronizationContext.Current;
            _keep = OnNativeEvent; // デリゲート保持
            tts_set_event_callback(_keep);
            tts_set_language(string.IsNullOrEmpty(language) ? "ja-JP" : language);
        }

        public static void SetLanguage(string lang) => tts_set_language(string.IsNullOrEmpty(lang) ? "ja-JP" : lang);

        /// <summary>identifier か language（例: "ja-JP"）。null で既定。</summary>
        public static void SetVoiceId(string identifierOrNull) => tts_set_voice_id(identifierOrNull);

        /// <summary>発話（rate は 0.5..1.5 推奨 / pitch 0.5..2.0 / volume 0..1）。queue=false で即時置き換え。</summary>
        public static bool Speak(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f,
            bool queue = false
        )
        {
            if (string.IsNullOrEmpty(text))
                return false;
            var rc = tts_speak(text, IntPtr.Zero, rate01, pitch, volume01, queue);
            if (rc != 0)
                Debug.LogError($"[TtsService] tts_speak failed: {rc}");
            return rc == 0;
        }

        public static void Stop() => tts_stop();

        public static bool IsSpeaking() => tts_is_speaking() == 1;

        /// <summary>利用可能な音声一覧（JSON: [{identifier, language, name}]）。失敗時 null。</summary>
        public static string? ListVoicesJson()
        {
            var p = tts_list_voices_json();
            if (p == IntPtr.Zero)
                return null;
            try
            {
                return Marshal.PtrToStringUTF8(p);
            }
            finally
            {
                tts_free(p);
            }
        }

        [AOT.MonoPInvokeCallback(typeof(EventCb))]
        static void OnNativeEvent(int ev)
        {
            void Raise()
            {
                switch (ev)
                {
                    case 0:
                        OnStart?.Invoke();
                        break;
                    case 1:
                        OnFinish?.Invoke();
                        break;
                    case 2:
                        OnCancel?.Invoke();
                        break;
                    default:
                        OnError?.Invoke();
                        break;
                }
            }
            var ctx = _main;
            if (ctx != null)
                ctx.Post(_ => Raise(), null);
            else
                Raise();
        }

        /// <summary>
        /// ネイティブTTSでPCMを生成して AudioClip にして返す（同期）。
        /// 失敗時は null。rate/pitch/volume は Speak と同一仕様。
        /// </summary>
        public static AudioClip? SynthesizeToClip(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f
        )
        {
            if (string.IsNullOrEmpty(text))
                return null;

            IntPtr mem;
            int frames;
            int sr;
            var rc = tts_synthesize_pcm(text, IntPtr.Zero, rate01, pitch, volume01, out mem, out frames, out sr);
            if (rc != 0 || mem == IntPtr.Zero || frames <= 0 || sr <= 0)
            {
                Debug.LogError($"[TtsService] tts_synthesize_pcm failed: {rc}");
                return null;
            }

            try
            {
                // Float32 モノラル
                var data = new float[frames];
                Marshal.Copy(mem, data, 0, frames);

                var clip = AudioClip.Create("tts", frames, 1, sr, false);
                clip.SetData(data, 0);
                return clip;
            }
            finally
            {
                tts_free(mem);
            }
        }

#else
        // ---- ここから iOS/mac 以外のスタブ実装 ------------------------------
        public static void Init(SynchronizationContext? mainContext = null, string language = "ja-JP") =>
            Debug.LogWarning("[TtsService] iOS/macOS 実機ビルドで有効になります（現在はスタブ）。");

        public static void SetLanguage(string lang)
        {
            Debug.LogWarning("[TtsService] このプラットフォームではネイティブTTSは無効（スタブ）。");
        }

        public static void SetVoiceId(string identifierOrNull)
        {
            Debug.LogWarning("[TtsService] このプラットフォームではネイティブTTSは無効（スタブ）。");
        }

        public static bool Speak(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f,
            bool queue = false
        )
        {
            Debug.LogWarning("[TtsService] このプラットフォームではネイティブTTSは無効（スタブ）。");
            return false;
        }

        public static void Stop()
        {
            Debug.LogWarning("[TtsService] このプラットフォームではネイティブTTSは無効（スタブ）。");
        }

        public static bool IsSpeaking() => false;

        public static string? ListVoicesJson() => null;

        public static AudioClip? SynthesizeToClip(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f
        )
        {
            Debug.LogWarning("[TtsService] このプラットフォームではネイティブTTSは無効（スタブ）。");
            return null;
        }
#endif
        // ---- ここまでスタブ実装 ---------------------------------------------
    }
}
