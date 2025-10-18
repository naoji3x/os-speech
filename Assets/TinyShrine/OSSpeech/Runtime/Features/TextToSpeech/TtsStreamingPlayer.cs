#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TinyShrine.OSSpeech.TextToSpeech
{
    // シンプルな PCM ストリーミングプレイヤー（16kHz/16bit/mono想定）
    // AudioSource と組み合わせてシーンに配置して使います。
    [RequireComponent(typeof(AudioSource))]
    public sealed class TtsStreamingPlayer : MonoBehaviour
    {
        [Tooltip("リングバッファの秒数（過去データ保持量）")]
        [Range(1, 10)]
        public int bufferSeconds = 3;

        private float[] buffer = Array.Empty<float>();
        private int writePos;
        private int readPos;
        private int capacity;
        private object gate = new object();
        private int inputSampleRate = 16000;
        private int inputChannels = 1;

        private AudioSource audioSource = null!;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;

            var fmt = TextToSpeechWindowsBridge.GetOutputPcmFormat();
            inputSampleRate = fmt.sampleRate;
            inputChannels = fmt.channels;
            if (fmt.bitsPerSample != 16)
            {
                Debug.LogWarning($"TTS PCM bitsPerSample={fmt.bitsPerSample} is not 16, playback may be invalid.");
            }

            capacity = Mathf.Max(1, bufferSeconds) * inputSampleRate * inputChannels;
            buffer = new float[capacity];
            writePos = 0;
            readPos = 0;
        }

        private void OnEnable()
        {
            TextToSpeechWindowsBridge.OnPcmChunk += OnChunk;
            TextToSpeechWindowsBridge.OnSynthesisComplete += OnComplete;
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        private void OnDisable()
        {
            TextToSpeechWindowsBridge.OnPcmChunk -= OnChunk;
            TextToSpeechWindowsBridge.OnSynthesisComplete -= OnComplete;
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        private void OnChunk(byte[] pcm)
        {
            // 16bit PCM リトルエンディアン → float に変換して書き込み
            lock (gate)
            {
                int samples = pcm.Length / 2;
                for (int i = 0; i < samples; i++)
                {
                    short s = (short)(pcm[2 * i] | (pcm[2 * i + 1] << 8));
                    buffer[writePos] = s / 32768f;
                    writePos = (writePos + 1) % capacity;
                    // 読み位置を追い越すときは読み側を1つ進めてドロップ
                    if (writePos == readPos)
                    {
                        readPos = (readPos + 1) % capacity;
                    }
                }
            }
        }

        private void OnComplete(int status)
        {
            // 必要であれば完了通知をログ
            if (status == 0)
            {
                Debug.Log("TTS synthesis complete");
            }
            else if (status < 0)
            {
                Debug.Log("TTS synthesis canceled");
            }
            else
            {
                Debug.LogWarning($"TTS synthesis error: 0x{status:X8}");
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            // Unity の出力サンプルレートと入力 16kHz が異なる場合、
            // ここでの単純な読み出しはピッチが変わる点に注意（簡易実装）
            lock (gate)
            {
                int len = data.Length;
                for (int i = 0; i < len; i += channels)
                {
                    float v = 0f;
                    if (readPos != writePos)
                    {
                        v = buffer[readPos];
                        readPos = (readPos + 1) % capacity;
                    }

                    // モノラルを全てのチャンネルに出力
                    for (int ch = 0; ch < channels; ch++)
                    {
                        data[i + ch] = v;
                    }
                }
            }
        }
    }
}
#endif
