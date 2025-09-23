using System;
using System.Threading;

namespace TinyShrine.OSSpeech.SpeechToText
{
    /// <summary>
    /// 音声認識サービスのインターフェース。部分結果・確定結果のイベント通知、初期化・開始・停止を提供します。
    /// </summary>
    public interface ISpeechToTextService : IDisposable
    {
        /// <summary>
        /// 途中経過（部分結果）。UIに逐次表示したいときに利用します。
        /// </summary>
        event Action<string> OnPartial;

        /// <summary>
        /// 確定結果。DB保存やLLM投入などはこちらで利用します。
        /// </summary>
        event Action<string> OnFinal;

        /// <summary>
        /// サービスを初期化します。
        /// </param>
        /// <param name="mainContext">UnityのメインスレッドのSynchronizationContext。</param>
        /// <param name="locale">認識言語（例: "ja-JP"）。</param>
        /// </summary>
        void Init(SynchronizationContext mainContext, string locale = "ja-JP");

        /// <summary>
        /// 音声認識を開始します。
        /// </summary>
        /// <returns>開始に成功した場合は true。</returns>
        bool Start();

        /// <summary>
        /// 音声認識を停止します。
        /// </summary>
        void Stop();
    }
}
