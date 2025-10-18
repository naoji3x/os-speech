#include "TextToSpeechBridge.h"
#include <windows.h>
#include <sapi.h>
#include <sphelper.h> // CSpStreamFormat, etc.
#include <atlbase.h>  // CComPtr
#include <string>
#include <fstream>
#include <vector>
#include <thread>
#include <atomic>

static CComPtr<ISpVoice> g_voice;
static bool g_comInitialized = false;
static std::wstring g_selectedVoiceName; // Init で選択したボイス名（説明）
static int g_selectedRate = 0;
static USHORT g_selectedVolume = 100;

// Streaming callbacks
static TTS_AudioChunkCallback g_onChunk = nullptr;
static TTS_CompleteCallback g_onComplete = nullptr;
static void *g_userData = nullptr;
static std::thread g_worker;
static std::atomic<bool> g_cancel{false};
static std::atomic<bool> g_busy{false};

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

    // PCM 出力のフォーマット（現在は 16kHz/16bit/mono 固定）
    int TTS_GetOutputPcmFormat(int *outSampleRate, int *outBitsPerSample, int *outChannels)
    {
        if (outSampleRate)
            *outSampleRate = 16000;
        if (outBitsPerSample)
            *outBitsPerSample = 16;
        if (outChannels)
            *outChannels = 1;
        return 0;
    }

    void TTS_SetStreamCallbacks(TTS_AudioChunkCallback onChunk, TTS_CompleteCallback onComplete, void *userData)
    {
        g_onChunk = onChunk;
        g_onComplete = onComplete;
        g_userData = userData;
    }

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
        // 設定を保持（未指定ならクリア）
        if (voiceName && *voiceName)
        {
            g_selectedVoiceName = voiceName;
        }
        else
        {
            g_selectedVoiceName.clear();
        }
        return 0;
    }

    // IStream 実装：受け取った WAV バイトを蓄積
    class MemoryWavSink : public IStream
    {
    public:
        ULONG m_ref{1};
        std::vector<uint8_t> m_data;
        ULARGE_INTEGER m_pos{0};

        // IUnknown
        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void **ppv) override
        {
            if (!ppv)
                return E_POINTER;
            if (riid == IID_IUnknown || riid == IID_IStream || riid == IID_ISequentialStream)
            {
                *ppv = static_cast<IStream *>(this);
                AddRef();
                return S_OK;
            }
            *ppv = nullptr;
            return E_NOINTERFACE;
        }
        ULONG STDMETHODCALLTYPE AddRef() override { return ++m_ref; }
        ULONG STDMETHODCALLTYPE Release() override
        {
            ULONG r = --m_ref;
            if (r == 0)
                delete this;
            return r;
        }

        // ISequentialStream
        HRESULT STDMETHODCALLTYPE Read(void *pv, ULONG cb, ULONG *pcbRead) override
        {
            // 読み出しは未使用
            if (pcbRead)
                *pcbRead = 0;
            return E_NOTIMPL;
        }
        HRESULT STDMETHODCALLTYPE Write(const void *pv, ULONG cb, ULONG *pcbWritten) override
        {
            if (!pv)
                return STG_E_INVALIDPOINTER;
            const uint8_t *p = static_cast<const uint8_t *>(pv);
            if (m_pos.QuadPart + cb > m_data.size())
            {
                m_data.resize(static_cast<size_t>(m_pos.QuadPart + cb));
            }
            memcpy(m_data.data() + m_pos.QuadPart, p, cb);
            m_pos.QuadPart += cb;
            if (pcbWritten)
                *pcbWritten = cb;
            return S_OK;
        }

        // IStream
        HRESULT STDMETHODCALLTYPE Seek(LARGE_INTEGER dlibMove, DWORD dwOrigin, ULARGE_INTEGER *plibNewPosition) override
        {
            LONGLONG base = 0;
            if (dwOrigin == STREAM_SEEK_SET)
                base = 0;
            else if (dwOrigin == STREAM_SEEK_CUR)
                base = static_cast<LONGLONG>(m_pos.QuadPart);
            else if (dwOrigin == STREAM_SEEK_END)
                base = static_cast<LONGLONG>(m_data.size());
            LONGLONG np = base + dlibMove.QuadPart;
            if (np < 0)
                return STG_E_INVALIDFUNCTION;
            m_pos.QuadPart = static_cast<ULONGLONG>(np);
            if (plibNewPosition)
                plibNewPosition->QuadPart = m_pos.QuadPart;
            return S_OK;
        }
        HRESULT STDMETHODCALLTYPE SetSize(ULARGE_INTEGER) override { return S_OK; }
        HRESULT STDMETHODCALLTYPE CopyTo(IStream *, ULARGE_INTEGER, ULARGE_INTEGER *, ULARGE_INTEGER *) override { return E_NOTIMPL; }
        HRESULT STDMETHODCALLTYPE Commit(DWORD) override { return S_OK; }
        HRESULT STDMETHODCALLTYPE Revert() override { return E_NOTIMPL; }
        HRESULT STDMETHODCALLTYPE LockRegion(ULARGE_INTEGER, ULARGE_INTEGER, DWORD) override { return E_NOTIMPL; }
        HRESULT STDMETHODCALLTYPE UnlockRegion(ULARGE_INTEGER, ULARGE_INTEGER, DWORD) override { return E_NOTIMPL; }
        HRESULT STDMETHODCALLTYPE Stat(STATSTG *pstatstg, DWORD) override
        {
            if (!pstatstg)
                return STG_E_INVALIDPOINTER;
            ZeroMemory(pstatstg, sizeof(*pstatstg));
            pstatstg->cbSize.QuadPart = m_data.size();
            pstatstg->type = STGTY_STREAM;
            return S_OK;
        }
        HRESULT STDMETHODCALLTYPE Clone(IStream **ppstm) override
        {
            if (!ppstm)
                return STG_E_INVALIDPOINTER;
            *ppstm = nullptr;
            return E_NOTIMPL;
        }
    };

    // WAV ヘッダを解析し、data チャンクを探す（簡易実装）
    static bool ExtractPcmFromWav(const std::vector<uint8_t> &wav, size_t &dataOffset, size_t &dataSize)
    {
        if (wav.size() < 44)
            return false;
        // 'RIFF' + size + 'WAVE'
        if (memcmp(wav.data(), "RIFF", 4) != 0 || memcmp(wav.data() + 8, "WAVE", 4) != 0)
            return false;
        size_t pos = 12; // 最初のチャンク
        while (pos + 8 <= wav.size())
        {
            const char *id = reinterpret_cast<const char *>(wav.data() + pos);
            uint32_t sz = *reinterpret_cast<const uint32_t *>(wav.data() + pos + 4);
            pos += 8;
            if (pos + sz > wav.size())
                return false;
            if (memcmp(id, "data", 4) == 0)
            {
                dataOffset = pos;
                dataSize = sz;
                return true;
            }
            pos += ((sz + 1) & ~1u); // 偶数境界にアライン
        }
        return false;
    }

    // WAV PCM をチャンクでコールバックに送る
    static void StreamPcmToCallback(const std::vector<uint8_t> &wav)
    {
        if (!g_onChunk && !g_onComplete)
            return;
        size_t off = 0, sz = 0;
        if (!ExtractPcmFromWav(wav, off, sz))
        {
            if (g_onComplete)
                g_onComplete(-10, g_userData); // WAV パース失敗
            return;
        }
        const size_t chunkBytes = 3200; // 16kHz * 2bytes * 1ch * 0.1s = 3200 bytes (100ms)
        size_t sent = 0;
        while (sent < sz && !g_cancel.load())
        {
            size_t n = (sz - sent > chunkBytes) ? chunkBytes : (sz - sent);
            if (g_onChunk)
            {
                g_onChunk(wav.data() + off + sent, static_cast<int>(n), g_userData);
            }
            sent += n;
            // 軽いスロットリング（任意）：音声再生に追従する場合は Sleep
            ::Sleep(50);
        }
        if (g_cancel.load())
        {
            if (g_onComplete)
                g_onComplete(-1, g_userData); // キャンセル
        }
        else
        {
            if (g_onComplete)
                g_onComplete(0, g_userData);
        }
    }

    int TTS_StartSpeak(const char *textUtf8)
    {
        if (!g_voice)
            return -1;
        if (!textUtf8)
            return -2;
        if (g_busy.load())
            return -3; // 既に実行中
        g_busy.store(true);
        g_cancel.store(false);

        // バックグラウンドで合成実行
        if (g_worker.joinable())
            g_worker.join();

        std::string textCopy = textUtf8; // スレッド用にコピー
        g_worker = std::thread([textCopy]()
                               {
            HRESULT hrCo = ::CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
            CComPtr<ISpVoice> localVoice;
            HRESULT hr = localVoice.CoCreateInstance(CLSID_SpVoice);
            if (FAILED(hr))
            {
                if (g_onComplete)
                    g_onComplete((int)hr, g_userData);
                g_busy.store(false);
                if (SUCCEEDED(hrCo)) ::CoUninitialize();
                return;
            }

            // 選択ボイス反映
            if (!g_selectedVoiceName.empty())
            {
                CComPtr<IEnumSpObjectTokens> cpEnum;
                ULONG count = 0;
                hr = SpEnumTokens(SPCAT_VOICES, nullptr, nullptr, &cpEnum);
                if (SUCCEEDED(hr)) hr = cpEnum->GetCount(&count);
                for (ULONG i = 0; SUCCEEDED(hr) && i < count; ++i)
                {
                    CComPtr<ISpObjectToken> tok;
                    if (SUCCEEDED(cpEnum->Next(1, &tok, nullptr)) && tok)
                    {
                        CSpDynamicString desc;
                        if (SUCCEEDED(SpGetDescription(tok, &desc)))
                        {
                            if (wcscmp(desc, g_selectedVoiceName.c_str()) == 0)
                            {
                                localVoice->SetVoice(tok);
                                break;
                            }
                        }
                    }
                }
            }

            // レート/ボリューム反映
            localVoice->SetRate(g_selectedRate);
            localVoice->SetVolume(g_selectedVolume);

            // 出力先：メモリ WAV
            CComPtr<IStream> memStream = new MemoryWavSink();
            CSpStreamFormat fmt;
            hr = fmt.AssignFormat(SPSF_16kHz16BitMono);
            if (SUCCEEDED(hr))
            {
                hr = localVoice->SetOutput(memStream, TRUE);
            }
            if (FAILED(hr))
            {
                if (g_onComplete)
                    g_onComplete((int)hr, g_userData);
                g_busy.store(false);
                if (SUCCEEDED(hrCo)) ::CoUninitialize();
                return;
            }

            // 合成（非同期）
            std::wstring textW = Utf8ToUtf16(textCopy.c_str());
            hr = localVoice->Speak(textW.c_str(), SPF_ASYNC, nullptr);
            if (FAILED(hr))
            {
                localVoice->SetOutput(nullptr, FALSE);
                if (g_onComplete)
                    g_onComplete((int)hr, g_userData);
                g_busy.store(false);
                if (SUCCEEDED(hrCo)) ::CoUninitialize();
                return;
            }

            // 完了 or キャンセル待ち
            while (!g_cancel.load())
            {
                // 20ms ごとに完了を待つ（S_OK=完了、S_FALSE=継続中）
                HRESULT hrw = localVoice->WaitUntilDone(20);
                if (hrw == S_OK)
                    break;
                if (FAILED(hrw))
                    break;
            }
            if (g_cancel.load())
            {
                localVoice->Speak(nullptr, SPF_PURGEBEFORESPEAK, nullptr);
            }

            // 出力を閉じてデータ取得
            localVoice->SetOutput(nullptr, FALSE);
            MemoryWavSink *raw = static_cast<MemoryWavSink *>(memStream.p);
            std::vector<uint8_t> wav = raw->m_data; // コピー

            if (!g_cancel.load())
            {
                StreamPcmToCallback(wav);
            }
            else
            {
                if (g_onComplete)
                    g_onComplete(-1, g_userData);
            }
            g_busy.store(false);
            if (SUCCEEDED(hrCo)) ::CoUninitialize(); });

        return 0;
    }

    void TTS_Cancel()
    {
        g_cancel.store(true);
        if (g_worker.joinable())
        {
            g_worker.join();
        }
        g_busy.store(false);
    }

    int TTS_SetRate(int rate)
    {
        if (!g_voice)
            return -1;
        g_selectedRate = rate;
        return (int)g_voice->SetRate(rate);
    }

    int TTS_SetVolume(int volume)
    {
        if (!g_voice)
            return -1;
        USHORT v = (USHORT)(volume < 0 ? 0 : (volume > 100 ? 100 : volume));
        g_selectedVolume = v;
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
        TTS_Cancel();
        g_voice.Release();
        if (g_comInitialized)
        {
            ::CoUninitialize();
            g_comInitialized = false;
        }
    }

} // extern "C"
