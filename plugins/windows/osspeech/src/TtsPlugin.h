#pragma once
#include <stdint.h>

#if defined(_WIN32)
#define TTS_API __declspec(dllexport)
#else
#define TTS_API
#endif

extern "C"
{
    // 初期化：voiceNameはnull可（既定ボイス使用）。戻り値0で成功（それ以外はHRESULT）
    TTS_API int TTS_Init(const wchar_t *voiceName /*nullable*/);

    // 話速（-10～10、SAPI既定レンジ）
    TTS_API int TTS_SetRate(int rate);

    // 音量（0～100）
    TTS_API int TTS_SetVolume(int volume);

    // UTF-8のテキストをWAV（メモリ）へ合成して返す。
    // outData/outSizeはDLLが確保。呼び出し側はTTS_Freeで解放。
    TTS_API int TTS_SynthesizeToWav(
        const char *textUtf8,
        uint8_t **outData,
        int *outSizeBytes);

    // メモリ解放（TTS_SynthesizeToWavで受け取ったポインタ用）
    TTS_API void TTS_Free(void *p);

    // 終了
    TTS_API void TTS_Shutdown();

} // extern "C"
