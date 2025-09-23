using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TinyShrine.OSSpeech.SpeechToText
{
    /// <summary>
    /// macOS Speech Framework のシンプルなラッパー
    /// NOTE: このクラスはIDisposableを実装しています。using文または明示的なDispose()でリソースを解放してください。
    /// </summary>
    public sealed class SpeechToTextAppleBridge : IDisposable
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string LIB = "__Internal";
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
        private const string LIB = "libOSSpeech"; // Plugins/macOS/libOSSpeech.dylibの実体名
#else
        private const string LIB = "libOSSpeech";
#endif

        // インスタンス/静的フィールド（static は先頭へ）
        private static ResultCb? staticCallback; // GC防止（static に保持）
        private static SpeechToTextAppleBridge? staticCurrent; // ネイティブ側が単一想定のため、現在のインスタンスにディスパッチ
        private readonly SynchronizationContext? mainContext;
        private bool disposed;

#pragma warning disable IDE1006, SA1300, CA2101 // 命名スタイル
        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void stt_set_callback(ResultCb cb);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int stt_request_authorization(); // 3 = Authorized

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int stt_set_locale([MarshalAs(UnmanagedType.LPUTF8Str)] string locale);

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern int stt_start();

        [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
        private static extern void stt_stop();
#pragma warning restore IDE1006, SA1300, CA2101 // 命名スタイル

        // ネイティブ→C# の入り口（static 必須）
        [AOT.MonoPInvokeCallback(typeof(ResultCb))]
        private static void StaticOnNativeResult(string text, bool isFinal)
        {
            var inst = staticCurrent;
            // if (inst == null || inst.disposed)
            if (inst?.disposed != false)
            {
                return;
            }
            inst.HandleNativeResult(text, isFinal);
        }

        // P/Invoke宣言（静的メンバー）
        // デリゲート定義（IL2CPP: bool は I1 指定必須）
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ResultCb(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            [MarshalAs(UnmanagedType.I1)] bool isFinal
        );

        // コンストラクタ
        public SpeechToTextAppleBridge(SynchronizationContext mainContext, string locale = "ja-JP")
        {
            locale = string.IsNullOrEmpty(locale) ? "ja-JP" : locale;
            this.mainContext = mainContext;

            // IL2CPP はインスタンスメソッドの関数ポインタをネイティブに渡せないため、static を渡す
            if (staticCallback == null)
            {
                staticCallback = StaticOnNativeResult;
            }
            staticCurrent = this; // 現在のインスタンスにディスパッチ
            stt_set_callback(staticCallback);

            var auth = stt_request_authorization(); // 3=Authorized
            if (auth != 3)
            {
                UnityEngine.Debug.LogWarning($"[SpeechToTextAppleBridge] Speech auth status: {auth}");
            }

            var rc = stt_set_locale(locale);
            if (rc != 0)
            {
                UnityEngine.Debug.LogWarning($"[SpeechToTextAppleBridge] set_locale failed: {rc}");
            }
            UnityEngine.Debug.LogWarning("[SpeechToTextAppleBridge] iOS/macOS 実機ビルドで有効になります。");
        }

        // イベント

        /// <summary>途中経過（部分結果）。UIに逐次表示したいときに。</summary>
        public event Action<string> OnPartial = static text => { };

        /// <summary>確定結果。DB保存やLLM投入などはこちらで。</summary>
        public event Action<string> OnFinal = static text => { };

        // パブリックメソッド
        public void SetLocale(string locale)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SpeechToTextAppleBridge));
            }

            locale = string.IsNullOrEmpty(locale) ? "ja-JP" : locale;
            var rc = stt_set_locale(locale);
            if (rc != 0)
            {
                UnityEngine.Debug.LogWarning($"[SpeechToTextAppleBridge] set_locale failed: {rc}");
            }
        }

        public bool Start()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(SpeechToTextAppleBridge));
            }

            var rc = stt_start();
            if (rc != 0)
            {
                UnityEngine.Debug.LogError($"[SpeechToTextAppleBridge] stt_start failed: {rc}");
                return false;
            }
            return true;
        }

        public void Stop()
        {
            if (disposed)
            {
                return;
            }

            stt_stop();
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
                    Stop(); // 音声認識を停止

                    // イベントハンドラーのクリア（既存のハンドラーをすべて削除）
                    if (OnPartial != null)
                    {
                        foreach (var handler in OnPartial.GetInvocationList())
                        {
                            OnPartial -= (Action<string>)handler;
                        }
                    }

                    if (OnFinal != null)
                    {
                        foreach (var handler in OnFinal.GetInvocationList())
                        {
                            OnFinal -= (Action<string>)handler;
                        }
                    }
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

        // インスタンス側の実処理
        private void HandleNativeResult(string text, bool isFinal)
        {
            if (disposed)
            {
                return;
            }

            // Unity APIはメインスレッドのみ安全。必要ならメインへディスパッチ。
            void Raise()
            {
                if (disposed)
                {
                    return;
                }

                if (isFinal)
                {
                    OnFinal?.Invoke(text);
                }
                else
                {
                    OnPartial?.Invoke(text);
                }
            }

            var ctx = mainContext;
            if (ctx != null)
            {
                ctx.Post(_ => Raise(), null);
            }
            else
            {
                Raise(); // 最悪フォールバック（自分でメインスレッド制御している場合を想定）
            }
        }
    }
}
