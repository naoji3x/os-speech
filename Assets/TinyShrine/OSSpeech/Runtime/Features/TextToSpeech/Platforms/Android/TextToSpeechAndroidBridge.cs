using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.TextToSpeech
{
    /// <summary>
    /// Android TextToSpeechBridge へのブリッジ。Java 側のコールバックを受け取り、Unity メインスレッドへ中継する。
    /// </summary>
    public sealed class TextToSpeechAndroidBridge : AndroidJavaProxy, IDisposable
    {
        // ---- Java 側シンボル ----
        private const string AndroidBridgeClass = "jp.tinyshrine.osspeech.TextToSpeechBridge";
        private const string CallbackInterface = "jp.tinyshrine.osspeech.TextToSpeechBridge$Callback";

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

        // ---- WAV読み込み（16bit PCM または float32 PCM に対応） ----
        private static AudioClip LoadWavAsClip(string path)
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            // RIFF ヘッダ確認
            if (new string(br.ReadChars(4)) != "RIFF")
            {
                throw new InvalidDataException("Not RIFF");
            }

            br.ReadInt32(); // file size
            if (new string(br.ReadChars(4)) != "WAVE")
            {
                throw new InvalidDataException("Not WAVE");
            }

            // fmt チャンクを探す
            int channels = 1;
            int sampleRate = 22050;
            int bitsPerSample = 16;
            int audioFormat = 1;

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
                        string msg = $"Unsupported WAV format: fmt={audioFormat}, bps={bitsPerSample}";
                        throw new InvalidDataException(msg);
                    }

                    // ステレオ→モノラル平均
                    if (channels == 2)
                    {
                        int frames = samples.Length / 2;
                        float[] mono = new float[frames];
                        for (int f = 0; f < frames; f++)
                        {
                            float l = samples[f * 2];
                            float r = samples[(f * 2) + 1];
                            mono[f] = (l + r) * 0.5f;
                        }
                        samples = mono;
                    }

                    var clip = AudioClip.Create("tts", samples.Length, 1, sampleRate, false);
                    clip.SetData(samples, 0);
                    return clip;
                }

                br.BaseStream.Position = next;
            }

            throw new InvalidDataException("WAV data chunk not found");
        }

        // ---- フィールド ----
        private readonly AndroidJavaClass bridgeClass;
        private readonly SynchronizationContext mainContext;
        private AndroidJavaObject? unityActivity;

        // Dispose の原子化（0=alive, 1=disposed）
        private int disposed;
        private bool IsDisposed => Volatile.Read(ref disposed) == 1;

        private bool TryBeginDispose() => Interlocked.Exchange(ref disposed, 1) == 0;

        // ---- イベント（null 許容）----
        public event Action? OnStart;
        public event Action? OnFinish;
        public event Action? OnCancel;
        public event Action? OnError;

        // ---- ctor ----
        public TextToSpeechAndroidBridge(SynchronizationContext mainContext, string language = "ja-JP")
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
            SetLocale(language);
        }

        // ---- Public API ----

        /// <summary>言語設定（例: "ja-JP", "en-US"）</summary>
        public void SetLocale(string locale)
        {
            ThrowIfDisposed();
            SafeCallStatic("setLocale", string.IsNullOrEmpty(locale) ? "ja-JP" : locale);
        }

        /// <summary>Androidでは Voice#getName() を identifier とみなします。</summary>
        public void SetVoiceId(string? identifierOrNull)
        {
            ThrowIfDisposed();
            SafeCallStatic("setVoiceId", identifierOrNull);
        }

        /// <summary>発話（rate は 0.5..1.5 推奨 / pitch 0.5..2.0 / volume 0..1）。queue=false で即時置き換え。</summary>
        public bool Speak(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f,
            bool queue = false
        )
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var rc = SafeCallStaticRet<int, string, object?, float, float, float, bool>(
                "speak",
                text,
                null,
                rate01,
                pitch,
                volume01,
                queue
            );
            if (rc != 0)
            {
                Debug.LogError($"[TextToSpeechAndroidBridge] speak failed: {rc}");
            }
            return rc == 0;
        }

        /// <summary>発話停止</summary>
        public void Stop()
        {
            ThrowIfDisposed();
            SafeCallStatic("stop");
        }

        /// <summary>現在発話中か？</summary>
        public bool IsSpeaking()
        {
            ThrowIfDisposed();
            return SafeCallStaticRet<bool>("isSpeaking");
        }

        /// <summary>利用可能な音声一覧（JSON: [{identifier, language, name}]）。失敗時 null。</summary>
        public string? ListVoicesJson()
        {
            ThrowIfDisposed();
            return SafeCallStaticRet<string>("listVoicesJson");
        }

        /// <summary>
        /// Android は TextToSpeech の制約で、いったん WAV ファイルに合成してから読み込みます。
        /// 返す AudioClip はモノラル。
        /// </summary>
        public AudioClip? SynthesizeToClip(string text, float rate01 = 1.0f, float pitch = 1.0f, float volume01 = 1.0f)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            string path = SafeCallStaticRet<string, string, object?, float, float, float, AndroidJavaObject?>(
                "synthesizeToFile",
                text,
                null,
                rate01,
                pitch,
                volume01,
                GetActivityOrNull()
            );

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError("[TextToSpeechAndroidBridge] synthesizeToFile failed.");
                return null;
            }

            try
            {
                return LoadWavAsClip(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TextToSpeechAndroidBridge] LoadWavAsClip failed: {e.Message}");
                return null;
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    // ignore
                }
            }
        }

        // ---- AndroidJavaProxy Callback ----
#pragma warning disable IDE1006, SA1300
        public void onEvent(int ev)
        {
            if (IsDisposed)
            {
                return;
            }

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

            InvokeOnMainThread(Raise);
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
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[TextToSpeechAndroidBridge] bridgeClass.Dispose exception: {ex.Message}");
                    }

                    try
                    {
                        unityActivity?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[TextToSpeechAndroidBridge] unityActivity.Dispose exception: {ex.Message}");
                    }

                    unityActivity = null;

                    // イベント購読を解除（GC支援）
                    OnStart = null;
                    OnFinish = null;
                    OnCancel = null;
                    OnError = null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TextToSpeechAndroidBridge] Dispose error: {e.Message}");
            }
        }

        // ---- Utility ----
        private void InvokeOnMainThread(Action action)
        {
            // 必ずメインスレッドへポスト（例外は PostInvoker 内でログ）
            mainContext.Post(PostInvoker, action);
        }

        private void DestroyInternal()
        {
            SafeCallStatic("destroy");
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(TextToSpeechAndroidBridge));
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
                Debug.LogError($"[TextToSpeechAndroidBridge] {method} failed: {e.Message}");
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
                Debug.LogError($"[TextToSpeechAndroidBridge] {method} failed: {e.Message}");
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
                Debug.LogError($"[TextToSpeechAndroidBridge] {method} failed: {e.Message}");
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
                Debug.LogError($"[TextToSpeechAndroidBridge] {method} failed: {e.Message}");
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
                Debug.LogError($"[TextToSpeechAndroidBridge] {method} failed: {e.Message}");
                return default!;
            }
        }

        private T SafeCallStaticRet<T, T1, T2, T3, T4, T5, T6>(string method, T1 a1, T2 a2, T3 a3, T4 a4, T5 a5, T6 a6)
        {
            try
            {
                return bridgeClass.CallStatic<T>(method, a1, a2, a3, a4, a5, a6);
            }
            catch (Exception e)
            {
                Debug.LogError($"[TextToSpeechAndroidBridge] {method} failed: {e.Message}");
                return default!;
            }
        }

        // (重複定義を削除)
    }
}
