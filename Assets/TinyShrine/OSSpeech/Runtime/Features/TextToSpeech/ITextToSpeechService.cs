using System;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.TextToSpeech
{
    /// <summary>
    /// テキスト読み上げサービスのインターフェース。
    /// </summary>
    /// <remarks>
    /// イベントは静的にして、インスタンスを意識せずに購読できるようにしています。
    /// </remarks>
    public interface ITextToSpeechService
    {
        /// <summary>
        /// 読み上げが開始されたときに発火するイベント。
        /// </summary>
        event Action OnStart;

        /// <summary>
        /// 読み上げが正常に完了したときに発火するイベント。
        /// </summary>
        event Action OnFinish;

        /// <summary>
        /// 読み上げがユーザー操作または API 呼び出しにより中断（キャンセル）されたときに発火するイベント。
        /// </summary>
        event Action OnCancel;

        /// <summary>
        /// 読み上げ処理中にエラーが発生したときに発火するイベント。
        /// </summary>
        event Action OnError;

        /// <summary>
        /// サービスを初期化します。
        /// </summary>
        /// <param name="mainContext">Unity のメインスレッド（メインループ）に紐づく <see cref="SynchronizationContext"/>。</param>
        /// <param name="locale">発話言語ロケール（例: "ja-JP"）。</param>
        void Init(SynchronizationContext mainContext, string locale = "ja-JP");

        /// <summary>
        /// 発話言語を切り替えます。
        /// </summary>
        /// <param name="lang">ロケールや言語コード（例: "ja-JP"）。</param>
        void SetLanguage(string lang);

        /// <summary>
        /// 使用するボイス（音声）を識別子で指定します。
        /// </summary>
        /// <param name="identifierOrNull">プラットフォーム依存のボイスID。null を渡すとデフォルトに戻します。</param>
        void SetVoiceId(string identifierOrNull);

        /// <summary>
        /// テキストを読み上げます。
        /// </summary>
        /// <param name="text">読み上げるテキスト。</param>
        /// <param name="rate01">再生速度（0〜1 付近、実装依存で上限超過を許容する場合あり）。</param>
        /// <param name="pitch">ピッチ（実装依存）。</param>
        /// <param name="volume01">音量（0〜1）。</param>
        /// <param name="queue">true でキューに追加、false で現在の読み上げを中断して即時再生。</param>
        /// <returns>読み上げ開始に成功したら true。</returns>
        bool Speak(string text, float rate01 = 1.0f, float pitch = 1.0f, float volume01 = 1.0f, bool queue = false);

        /// <summary>
        /// 進行中の読み上げを停止（キャンセル）します。
        /// </summary>
        void Stop();

        /// <summary>
        /// 現在読み上げ中かどうかを返します。
        /// </summary>
        /// <returns>読み上げ中なら true。</returns>
        bool IsSpeaking();

        /// <summary>
        /// 利用可能なボイス一覧を JSON 文字列で返します（プラットフォーム依存のフォーマット）。
        /// </summary>
        /// <returns>ボイス一覧の JSON。取得できない場合は null。</returns>
        string? ListVoicesJson();

        /// <summary>
        /// テキストを合成して <see cref="AudioClip"/> を生成します（同期または擬似同期）。
        /// </summary>
        /// <param name="text">合成するテキスト。</param>
        /// <param name="rate01">再生速度（0〜1 付近）。</param>
        /// <param name="pitch">ピッチ。</param>
        /// <param name="volume01">音量（0〜1）。</param>
        /// <returns>生成された <see cref="AudioClip"/>。失敗時は null。</returns>
        AudioClip? SynthesizeToClip(string text, float rate01 = 1.0f, float pitch = 1.0f, float volume01 = 1.0f);
    }
}
