#include "TtsPlugin.h"
#include <windows.h>
#include <sapi.h>
#include <sphelper.h> // CSpStreamFormat, etc.
#include <atlbase.h>  // CComPtr
#include <string>
#include <fstream>

static CComPtr<ISpVoice> g_voice;
static bool g_comInitialized = false;

static std::wstring Utf8ToUtf16(const char *u8)
{
    if (!u8)
        return L"";
    int n = MultiByteToWideChar(CP_UTF8, 0, u8, -1, nullptr, 0);
    if (n <= 0)
        return L"";
    std::wstring w(n - 1, L'\0');
    MultiByteToWideChar(CP_UTF8, 0, u8, -1, &w[0], n);
    return w;
}

static std::wstring MakeTempWavPath()
{
    wchar_t tmpPath[MAX_PATH] = {};
    ::GetTempPathW(MAX_PATH, tmpPath);
    wchar_t tmpFile[MAX_PATH] = {};
    ::GetTempFileNameW(tmpPath, L"TTS", 0, tmpFile);
    // 拡張子を .wav に
    std::wstring p = tmpFile;
    size_t dot = p.find_last_of(L'.');
    if (dot != std::wstring::npos)
        p.erase(dot);
    p += L".wav";
    return p;
}

extern "C"
{

    int TTS_Init(const wchar_t *voiceName)
    {
        HRESULT hr = ::CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        if (SUCCEEDED(hr) || hr == RPC_E_CHANGED_MODE)
        {
            g_comInitialized = true;
        }
        else
        {
            return (int)hr;
        }

        hr = g_voice.CoCreateInstance(CLSID_SpVoice);
        if (FAILED(hr))
            return (int)hr;

        // ボイス選択（省略可：指定なしなら既定）
        if (voiceName && *voiceName)
        {
            // ISpObjectTokenCategory で "HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Speech\\Voices" を列挙
            CComPtr<IEnumSpObjectTokens> cpEnum;
            ULONG count = 0;
            hr = SpEnumTokens(SPCAT_VOICES, nullptr, nullptr, &cpEnum);
            if (SUCCEEDED(hr))
                hr = cpEnum->GetCount(&count);
            for (ULONG i = 0; SUCCEEDED(hr) && i < count; ++i)
            {
                CComPtr<ISpObjectToken> tok;
                if (SUCCEEDED(cpEnum->Next(1, &tok, nullptr)) && tok)
                {
                    CSpDynamicString desc;
                    if (SUCCEEDED(SpGetDescription(tok, &desc)))
                    {
                        if (wcscmp(desc, voiceName) == 0)
                        {
                            g_voice->SetVoice(tok);
                            break;
                        }
                    }
                }
            }
        }
        return 0;
    }

    int TTS_SetRate(int rate)
    {
        if (!g_voice)
            return -1;
        return (int)g_voice->SetRate(rate);
    }

    int TTS_SetVolume(int volume)
    {
        if (!g_voice)
            return -1;
        USHORT v = (USHORT)(volume < 0 ? 0 : (volume > 100 ? 100 : volume));
        return (int)g_voice->SetVolume(v);
    }

    int TTS_SynthesizeToWav(const char *textUtf8, uint8_t **outData, int *outSizeBytes)
    {
        if (!g_voice)
            return -1;
        if (!textUtf8 || !outData || !outSizeBytes)
            return -2;

        std::wstring text = Utf8ToUtf16(textUtf8);
        std::wstring wavPath = MakeTempWavPath();

        // 出力フォーマット（16kHz/16bit/mono を例に。必要に応じて変更）
        CSpStreamFormat fmt;
        HRESULT hr = fmt.AssignFormat(SPSF_16kHz16BitMono);
        if (FAILED(hr))
            return (int)hr;

        // SpFileStream でWAVへ書き出し
        CComPtr<ISpStream> spStream;
        hr = SPBindToFile(wavPath.c_str(), SPFM_CREATE_ALWAYS, &spStream,
                          &fmt.FormatId(), fmt.WaveFormatExPtr(), 0);
        if (FAILED(hr))
            return (int)hr;

        hr = g_voice->SetOutput(spStream, TRUE);
        if (FAILED(hr))
            return (int)hr;

        hr = g_voice->Speak(text.c_str(), SPF_DEFAULT, nullptr);
        if (FAILED(hr))
            return (int)hr;

        g_voice->WaitUntilDone(INFINITE);
        spStream->Close();
        g_voice->SetOutput(nullptr, FALSE); // 元に戻す

        // WAV読み込み
        std::ifstream ifs(wavPath, std::ios::binary | std::ios::ate);
        if (!ifs)
        {
            // 失敗時、ファイルが作れていない
            DeleteFileW(wavPath.c_str());
            return -3;
        }
        std::streamsize size = ifs.tellg();
        ifs.seekg(0, std::ios::beg);

        uint8_t *buf = (uint8_t *)::CoTaskMemAlloc((SIZE_T)size);
        if (!buf)
        {
            DeleteFileW(wavPath.c_str());
            return -4;
        }
        if (!ifs.read(reinterpret_cast<char *>(buf), size))
        {
            ::CoTaskMemFree(buf);
            DeleteFileW(wavPath.c_str());
            return -5;
        }
        ifs.close();
        DeleteFileW(wavPath.c_str());

        *outData = buf;
        *outSizeBytes = (int)size;
        return 0;
    }

    void TTS_Free(void *p)
    {
        if (p)
            ::CoTaskMemFree(p);
    }

    void TTS_Shutdown()
    {
        g_voice.Release();
        if (g_comInitialized)
        {
            ::CoUninitialize();
            g_comInitialized = false;
        }
    }

} // extern "C"
