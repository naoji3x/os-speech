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
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using System.IO;
#endif

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
                Debug.LogError($"[TextToSpeechService] tts_speak failed: {rc}");
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
#elif UNITY_ANDROID && !UNITY_EDITOR

        static AndroidJavaClass bridge;
        static AndroidJavaObject activity;

        class CallbackProxy : AndroidJavaProxy
        {
            public CallbackProxy()
                : base("jp.tinyshrine.osspeech.TextToSpeechBridge$Callback") { }

            public void onEvent(int ev)
            {
                // iOS/mac のイベントに合わせる
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
        }

        public static void Init(SynchronizationContext? mainContext = null, string language = "ja-JP")
        {
            Debug.Log("[TextToSpeechService] Android TextToSpeech init");
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            bridge = new AndroidJavaClass("jp.tinyshrine.osspeech.TextToSpeechBridge");
            Debug.Log("[TextToSpeechService] Android TextToSpeech init" + bridge);
            bridge.CallStatic("init", activity, new CallbackProxy());
            if (!string.IsNullOrEmpty(language))
                SetLanguage(language);
        }

        public static void SetLanguage(string lang)
        {
            bridge?.CallStatic("setLanguage", string.IsNullOrEmpty(lang) ? "ja-JP" : lang);
        }

        /// <summary>Androidでは Voice#getName() を identifier とみなします。</summary>
        public static void SetVoiceId(string identifierOrNull)
        {
            bridge?.CallStatic("setVoiceId", identifierOrNull);
        }

        public static bool Speak(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f,
            bool queue = false
        )
        {
            if (string.IsNullOrEmpty(text) || bridge == null)
                return false;
            var rc = bridge.CallStatic<int>(
                "speak",
                text, /*voiceOrLocale*/
                null,
                rate01,
                pitch,
                volume01,
                queue
            );
            if (rc != 0)
                Debug.LogError($"[TextToSpeechService] Android speak failed: {rc}");
            return rc == 0;
        }

        public static void Stop() => bridge?.CallStatic("stop");

        public static bool IsSpeaking => bridge != null && bridge.CallStatic<bool>("isSpeaking");

        public static string ListVoicesJson()
        {
            return bridge?.CallStatic<string>("listVoicesJson");
        }

        /// <summary>
        /// Androidは TextToSpeech の制約で、いったん WAV ファイルに合成してから読み込みます。
        /// 返す AudioClip はモノラル（必要に応じて拡張可）。
        /// </summary>
        public static AudioClip SynthesizeToClip(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f
        )
        {
            if (string.IsNullOrEmpty(text) || bridge == null)
                return null;
            string path = bridge.CallStatic<string>(
                "synthesizeToFile",
                text, /*voiceOrLocale*/
                null,
                rate01,
                pitch,
                volume01,
                activity
            );
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError("[TextToSpeechService] synthesizeToFile failed.");
                return null;
            }
            try
            {
                return LoadWavAsClip(path);
            }
            finally
            {
                // 生成ファイルは不要なら削除
                try
                {
                    File.Delete(path);
                }
                catch { }
            }
        }

        // ---- WAV読み込み（16bit PCM または float32 PCM に対応） ----
        static AudioClip LoadWavAsClip(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            // RIFF ヘッダ確認
            if (new string(br.ReadChars(4)) != "RIFF")
                throw new Exception("Not RIFF");
            br.ReadInt32(); // file size
            if (new string(br.ReadChars(4)) != "WAVE")
                throw new Exception("Not WAVE");

            // fmt チャンクを探す
            int channels = 1,
                sampleRate = 22050,
                bitsPerSample = 16,
                audioFormat = 1;
            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                string chunk = new string(br.ReadChars(4));
                int size = br.ReadInt32();
                long next = br.BaseStream.Position + size;

                if (chunk == "fmt ")
                {
                    audioFormat = br.ReadInt16(); // 1=PCM, 3=IEEE float
                    channels = br.ReadInt16();
                    sampleRate = br.ReadInt32();
                    br.ReadInt32(); // byteRate
                    br.ReadInt16(); // blockAlign
                    bitsPerSample = br.ReadInt16();
                    // 余り読み飛ばし
                }
                else if (chunk == "data")
                {
                    byte[] data = br.ReadBytes(size);

                    // PCM -> float[]
                    float[] samples;
                    if (audioFormat == 1 && bitsPerSample == 16)
                    {
                        int count = size / 2;
                        samples = new float[count];
                        for (int i = 0; i < count; i++)
                        {
                            short s = BitConverter.ToInt16(data, i * 2);
                            samples[i] = s / 32768f;
                        }
                    }
                    else if (audioFormat == 3 && bitsPerSample == 32)
                    {
                        int count = size / 4;
                        samples = new float[count];
                        Buffer.BlockCopy(data, 0, samples, 0, size);
                    }
                    else
                    {
                        throw new Exception($"Unsupported WAV format: fmt={audioFormat}, bps={bitsPerSample}");
                    }

                    // ステレオ→モノラル平均
                    if (channels == 2)
                    {
                        int frames = samples.Length / 2;
                        float[] mono = new float[frames];
                        for (int f = 0; f < frames; f++)
                            mono[f] = 0.5f * (samples[f * 2] + samples[f * 2 + 1]);
                        samples = mono;
                    }

                    var clip = AudioClip.Create("tts", samples.Length, 1, sampleRate, false);
                    clip.SetData(samples, 0);
                    return clip;
                }

                br.BaseStream.Position = next;
            }
            throw new Exception("WAV data chunk not found");
        }
#else
        // ---- ここから iOS/mac 以外のスタブ実装 ------------------------------
        public static void Init(SynchronizationContext? mainContext = null, string language = "ja-JP") =>
            Debug.LogWarning("[TextToSpeechService] iOS/macOS 実機ビルドで有効になります（現在はスタブ）。");

        public static void SetLanguage(string lang)
        {
            Debug.LogWarning("[TextToSpeechService] このプラットフォームではネイティブTTSは無効（スタブ）。");
        }

        public static void SetVoiceId(string identifierOrNull)
        {
            Debug.LogWarning("[TextToSpeechService] このプラットフォームではネイティブTTSは無効（スタブ）。");
        }

        public static bool Speak(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f,
            bool queue = false
        )
        {
            Debug.LogWarning("[TextToSpeechService] このプラットフォームではネイティブTTSは無効（スタブ）。");
            return false;
        }

        public static void Stop()
        {
            Debug.LogWarning("[TextToSpeechService] このプラットフォームではネイティブTTSは無効（スタブ）。");
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
            Debug.LogWarning("[TextToSpeechService] このプラットフォームではネイティブTTSは無効（スタブ）。");
            return null;
        }
#endif
        // ---- ここまでスタブ実装 ---------------------------------------------
    }
}
