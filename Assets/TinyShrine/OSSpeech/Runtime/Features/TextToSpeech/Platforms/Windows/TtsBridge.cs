using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Unity ↔ ネイティブ TTS(DLL) ブリッジ。適切な例外を投げるように整理。
/// </summary>
namespace TinyShrine.OSSpeech.TextToSpeech
{
    public static class TtsBridge
    {
        private const string DLL = "TtsPlugin"; // Assets/Plugins/x86_64/TtsPlugin.dll

        /// <summary>
        /// 初期化。voiceName は SAPI のボイス名（null で既定ボイス）。
        /// </summary>
        public static void Init(string? voiceName = null, int rate = 0, int volume = 100)
        {
            // ここで DLL が見つからない等の場合、DllNotFoundException / EntryPointNotFoundException が上がる
            int hr = TTS_Init(voiceName);
            ThrowIfFailed(hr, nameof(TTS_Init));

            hr = TTS_SetRate(rate);
            ThrowIfFailed(hr, nameof(TTS_SetRate));

            hr = TTS_SetVolume(volume);
            ThrowIfFailed(hr, nameof(TTS_SetVolume));
        }

        /// <summary>
        /// テキストを WAV に合成して AudioClip を返す。
        /// </summary>
        public static AudioClip SpeakToClip(string text, string clipName = "TTS")
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.Length == 0)
            {
                throw new ArgumentException("Text must not be empty.", nameof(text));
            }

            int hr = TTS_SynthesizeToWav(text, out var p, out var size);
            ThrowIfFailed(hr, nameof(TTS_SynthesizeToWav));

            try
            {
                if (size <= 44) // 最小のWAVヘッダすら満たさない
                {
                    throw new InvalidDataException("Synthesized WAV size is too small.");
                }

                byte[] wav = new byte[size];
                Marshal.Copy(p, wav, 0, size);

                int dataOffset = FindDataChunkOffset(wav);
                int sampleRate = ParseSampleRate(wav);
                short channels = ParseChannels(wav);
                short bits = ParseBitsPerSample(wav);

                if (channels <= 0 || (channels != 1 && channels != 2))
                {
                    throw new NotSupportedException(
                        $"Unsupported channel count: {channels} (only mono or stereo supported)."
                    );
                }

                if (bits != 16)
                {
                    throw new NotSupportedException(
                        $"Unsupported bits-per-sample: {bits} (only 16-bit PCM supported)."
                    );
                }

                int bytes = wav.Length - dataOffset;
                if (bytes <= 0 || (bytes % 2) != 0)
                {
                    throw new InvalidDataException("WAV data chunk size is invalid.");
                }

                int totalSamples = bytes / 2; // 16-bit → 2 bytes per sample (interleaved if stereo)
                float[] pcm = new float[totalSamples];
                for (int i = 0; i < totalSamples; i++)
                {
                    short s = BitConverter.ToInt16(wav, dataOffset + (i * 2));
                    pcm[i] = s / 32768f;
                }

                int frames = totalSamples / channels;
                var clip = AudioClip.Create(clipName, frames, channels, sampleRate, false);
                clip.SetData(pcm, 0);
                return clip;
            }
            finally
            {
                // ネイティブ側が CoTaskMemAlloc したメモリを必ず解放
                if (p != IntPtr.Zero)
                {
                    TTS_Free(p);
                }
            }
        }

        public static void Shutdown() => TTS_Shutdown();

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int TTS_Init(string? voiceName);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TTS_SetRate(int rate);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TTS_SetVolume(int volume);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern int TTS_SynthesizeToWav(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string? textUtf8,
            out IntPtr outData,
            out int outSizeBytes
        );

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void TTS_Free(IntPtr p);

        [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void TTS_Shutdown();

        /// <summary>
        /// ネイティブの HRESULT/戻り値が失敗のときに適切な例外を投げる。
        /// </summary>
        private static void ThrowIfFailed(int hr, string operation)
        {
            if (hr == 0)
            {
                return;
            }

            // 負の戻り値はネイティブ側の「独自エラー」として扱う
            switch (hr)
            {
                case -1:
                    throw new InvalidOperationException($"{operation} failed: TTS engine is not initialized.");
                case -2:
                    throw new ArgumentException($"{operation} failed: invalid argument.");
                case -3:
                    throw new IOException($"{operation} failed: could not create or open temp WAV file.");
                case -4:
                    throw new InsufficientMemoryException($"{operation} failed: memory allocation failed.");
                case -5:
                    throw new IOException($"{operation} failed: failed to read synthesized WAV.");
            }

            // それ以外は HRESULT 扱い。ランタイムが適切な例外型（主に COMException）を作る
            // ExternalException は自分で投げない
            Marshal.ThrowExceptionForHR(hr);
        }

        private static int FindDataChunkOffset(byte[] wav)
        {
            // "data" チャンクを雑に探索（より厳密にするなら RIFF/Chunk 解析を実装）
            for (int i = 12; i < wav.Length - 8; i++)
            {
                if (
                    wav[i] == (byte)'d'
                    && wav[i + 1] == (byte)'a'
                    && wav[i + 2] == (byte)'t'
                    && wav[i + 3] == (byte)'a'
                )
                {
                    return i + 8;
                }
            }
            throw new InvalidDataException("WAV data chunk not found.");
        }

        private static int ParseSampleRate(byte[] wav)
        {
            // fmt chunk の sampleRate はオフセット 24（RIFF 先頭から）
            return BitConverter.ToInt32(wav, 24);
        }

        private static short ParseChannels(byte[] wav)
        {
            // fmt chunk のチャンネル数はオフセット 22
            return BitConverter.ToInt16(wav, 22);
        }

        private static short ParseBitsPerSample(byte[] wav)
        {
            // fmt chunk の bitsPerSample はオフセット 34
            return BitConverter.ToInt16(wav, 34);
        }
    }
}
