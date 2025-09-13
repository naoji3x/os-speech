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

#elif UNITY_ANDROID && !UNITY_EDITOR

        static AndroidJavaClass _bridge;
        static AndroidJavaObject _activity;

        class CallbackProxy : AndroidJavaProxy
        {
            public CallbackProxy()
                : base("jp.tinyshrine.osspeech.TtsBridge$Callback") { }

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

        public static void Init(SynchronizationContext mainContext = null, string language = "ja-JP")
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            _activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _bridge = new AndroidJavaClass("jp.tinyshrine.osspeech.TtsBridge");
            _bridge.CallStatic("init", _activity, new CallbackProxy());
            if (!string.IsNullOrEmpty(language))
                SetLanguage(language);
        }

        public static void SetLanguage(string lang)
        {
            _bridge?.CallStatic("setLanguage", string.IsNullOrEmpty(lang) ? "ja-JP" : lang);
        }

        /// <summary>Androidでは Voice#getName() を identifier とみなします。</summary>
        public static void SetVoiceId(string identifierOrNull)
        {
            _bridge?.CallStatic("setVoiceId", identifierOrNull);
        }

        public static bool Speak(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f,
            bool queue = false
        )
        {
            if (string.IsNullOrEmpty(text) || _bridge == null)
                return false;
            var rc = _bridge.CallStatic<int>(
                "speak",
                text, /*voiceOrLocale*/
                null,
                rate01,
                pitch,
                volume01,
                queue
            );
            if (rc != 0)
                Debug.LogError($"[TtsService] Android speak failed: {rc}");
            return rc == 0;
        }

        public static void Stop() => _bridge?.CallStatic("stop");

        public static bool IsSpeaking => _bridge != null && _bridge.CallStatic<bool>("isSpeaking");

        public static string ListVoicesJson()
        {
            return _bridge?.CallStatic<string>("listVoicesJson");
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
            if (string.IsNullOrEmpty(text) || _bridge == null)
                return null;
            string path = _bridge.CallStatic<string>(
                "synthesizeToFile",
                text, /*voiceOrLocale*/
                null,
                rate01,
                pitch,
                volume01,
                _activity
            );
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError("[TtsService] synthesizeToFile failed.");
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
