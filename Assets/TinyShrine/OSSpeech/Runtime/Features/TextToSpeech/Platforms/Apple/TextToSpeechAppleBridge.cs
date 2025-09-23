using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.TextToSpeech
{
    public class TextToSpeechAppleBridge : IDisposable
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string LIB = "__Internal";
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        private const string LIB = "libOSSpeech"; // Plugins/macOS/libOSSpeech.dylibの実体名
#else
        private const string LIB = "libOSSpeech";
#endif
        private bool disposed;
        private Action? onStart;
        private Action? onFinish;
        private Action? onCancel;
        private Action? onError;

        public event Action OnStart
        {
            add => onStart += value;
            remove => onStart -= value;
        }
        public event Action OnFinish
        {
            add => onFinish += value;
            remove => onFinish -= value;
        }
        public event Action OnCancel
        {
            add => onCancel += value;
            remove => onCancel -= value;
        }
        public event Action OnError
        {
            add => onError += value;
            remove => onError -= value;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void EventCb(int ev); // 0=Start,1=Finish,2=Cancel,5=Error
#pragma warning disable IDE1006, SA1300, CA2101 // 命名スタイル
        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void tts_set_event_callback(EventCb cb);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tts_set_language([MarshalAs(UnmanagedType.LPUTF8Str)] string lang);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tts_set_voice_id([MarshalAs(UnmanagedType.LPUTF8Str)] string id);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tts_is_speaking();

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void tts_stop();

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tts_speak(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            IntPtr voiceOrNull,
            float rate01,
            float pitch,
            float volume01,
            [MarshalAs(UnmanagedType.I1)] bool queue
        );

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
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

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void tts_free(IntPtr p);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tts_list_voices_json();
#pragma warning restore IDE1006, SA1300, CA2101 // 命名スタイル

        private static EventCb? staticCallback; // GC対策（static に保持）
        private static TextToSpeechAppleBridge? staticCurrent; // ネイティブ側が単一想定のため、現在のインスタンスにディスパッチ

        private readonly SynchronizationContext? mainContext;

        [AOT.MonoPInvokeCallback(typeof(EventCb))]
        private static void StaticOnNativeEvent(int ev)
        {
            if (staticCurrent?.disposed == false)
            {
                staticCurrent.OnNativeEvent(ev);
            }
        }

        public TextToSpeechAppleBridge(SynchronizationContext mainContext, string language = "ja-JP")
        {
            this.mainContext = mainContext;

            // IL2CPP はインスタンスメソッドの関数ポインタをネイティブに渡せないため、static を渡す
            if (staticCallback == null)
            {
                staticCallback = StaticOnNativeEvent; // デリゲート保持
            }
            staticCurrent = this; // 現在のインスタンスにディスパッチ

            tts_set_event_callback(staticCallback);
            var result = tts_set_language(string.IsNullOrEmpty(language) ? "ja-JP" : language);
        }

        public static void SetLanguage(string lang)
        {
            var result = tts_set_language(string.IsNullOrEmpty(lang) ? "ja-JP" : lang);
        }

        /// <summary>identifier か language（例: "ja-JP"）。null で既定。</summary>
        public static void SetVoiceId(string identifierOrNull)
        {
            var result = tts_set_voice_id(identifierOrNull);
        }

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
            {
                return false;
            }

            var rc = tts_speak(text, IntPtr.Zero, rate01, pitch, volume01, queue);
            if (rc != 0)
            {
                Debug.LogError($"[TextToSpeechService] tts_speak failed: {rc}");
            }

            return rc == 0;
        }

        public static void Stop() => tts_stop();

        public static bool IsSpeaking() => tts_is_speaking() == 1;

        /// <summary>利用可能な音声一覧（JSON: [{identifier, language, name}]）。失敗時 null。</summary>
        public static string? ListVoicesJson()
        {
            var p = tts_list_voices_json();
            if (p == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUTF8(p);
            }
            finally
            {
                tts_free(p);
            }
        }

        /// <summary>
        /// ネイティブTTSでPCMを生成して AudioClip にして返す（同期）。
        /// 失敗時は null。rate/pitch/volume は Speak と同一仕様。
        /// </summary>
        public AudioClip? SynthesizeToClip(string text, float rate01 = 1.0f, float pitch = 1.0f, float volume01 = 1.0f)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            IntPtr mem;
            int frames;
            int sr;
            var rc = tts_synthesize_pcm(text, IntPtr.Zero, rate01, pitch, volume01, out mem, out frames, out sr);
            if (rc != 0 || mem == IntPtr.Zero || frames <= 0 || sr <= 0)
            {
                Debug.LogError($"[TextToSpeechService] tts_synthesize_pcm failed: {rc}");
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

        // IDisposable implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // マネージドリソースの解放
                    Stop(); // 発話を停止
                    onStart = null;
                    onFinish = null;
                    onCancel = null;
                    onError = null;
                }

                // アンマネージドリソースの解放
                // ネイティブ側のコールバックをクリア（可能であれば）
                disposed = true;

                // このインスタンスが現行ディスパッチ先なら解除
                if (ReferenceEquals(staticCurrent, this))
                {
                    staticCurrent = null;
                }
            }
        }

        private void OnNativeEvent(int ev)
        {
            void Raise()
            {
                switch (ev)
                {
                    case 0:
                        onStart?.Invoke();
                        break;
                    case 1:
                        onFinish?.Invoke();
                        break;
                    case 2:
                        onCancel?.Invoke();
                        break;
                    default:
                        onError?.Invoke();
                        break;
                }
            }
            var ctx = mainContext;
            if (ctx != null)
            {
                ctx.Post(_ => Raise(), null);
            }
            else
            {
                Raise();
            }
        }
    }
}
