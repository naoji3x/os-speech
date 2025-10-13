#pragma once
#include <stdint.h>

#if defined(_WIN32)
#define TTS_API __declspec(dllexport)
#else
#define TTS_API
#endif

extern "C"
{
    // ============================
    // Streaming API (Unity 用)
    // ============================
    // PCM チャンク受け取りコールバック
    // data: リトルエンディアン PCM（16bit）
    // sizeBytes: data のバイト数（2 の倍数）
    // userData: 呼び出し元が登録した任意ポインタ
    typedef void (*TTS_AudioChunkCallback)(const uint8_t *data, int sizeBytes, void *userData);

    // 完了通知コールバック
    // status: 0=成功、負数=ユーザー起因のキャンセル、その他=HRESULT/エラーコード
    typedef void (*TTS_CompleteCallback)(int status, void *userData);

    // 出力フォーマット（現在は固定：16kHz/16bit/mono）を取得
    // 返り値0=成功
    TTS_API int TTS_GetOutputPcmFormat(int *outSampleRate, int *outBitsPerSample, int *outChannels);

    // チャンク受け取り/完了通知のコールバックを登録
    // userData はそのまま各コールバックへ渡されます
    TTS_API void TTS_SetStreamCallbacks(TTS_AudioChunkCallback onChunk, TTS_CompleteCallback onComplete, void *userData);

    // 非同期でテキスト合成を開始（コールバックで逐次ストリーミング）
    // 返り値0=受理、<0=エラー/HRESULT
    TTS_API int TTS_SpeakAsync(const char *textUtf8);

    // 再生/合成をキャンセル（コールバックの status に負値を返す）
    TTS_API void TTS_Cancel();

    // 初期化：voiceNameはnull可（既定ボイス使用）。戻り値0で成功（それ以外はHRESULT）
    TTS_API int TTS_Init(const wchar_t *voiceName /*nullable*/);

    // 話速（-10～10、SAPI既定レンジ）
    TTS_API int TTS_SetRate(int rate);

    // 音量（0～100）
    TTS_API int TTS_SetVolume(int volume);

    // [Deprecated] メモリWAV出力 API（内部で一時ファイルを経由）
    // UTF-8のテキストをWAV（メモリ）へ合成して返す。outData/outSize は DLL が確保。呼び出し側は TTS_Free で解放。
    TTS_API int TTS_SynthesizeToWav(
        const char *textUtf8,
        uint8_t **outData,
        int *outSizeBytes);

    // メモリ解放（TTS_SynthesizeToWavで受け取ったポインタ用）
    TTS_API void TTS_Free(void *p);

    // 終了
    TTS_API void TTS_Shutdown();

} // extern "C"
