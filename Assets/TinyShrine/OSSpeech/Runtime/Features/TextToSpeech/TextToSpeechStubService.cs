using System;
using System.Threading;
using UnityEngine;

namespace TinyShrine.OSSpeech.TextToSpeech
{
    /// <summary>
    /// TextToSpeech のプラットフォーム非対応時に利用される No-Op 実装。
    /// </summary>
    public sealed class TextToSpeechStubService : ITextToSpeechService
    {
        public event Action OnStart = static () => { };
        public event Action OnFinish = static () => { };
        public event Action OnCancel = static () => { };
        public event Action OnError = static () => { };

        public void Init(SynchronizationContext mainContext, string locale = "ja-JP")
        {
            Debug.Log("[TextToSpeechNoOpService] Init called (No-Op)");
        }

        public void SetLanguage(string lang) => Debug.Log("[TextToSpeechNoOpService] SetLanguage ignored");

        public void SetVoiceId(string identifierOrNull) => Debug.Log("[TextToSpeechNoOpService] SetVoiceId ignored");

        public bool Speak(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f,
            bool queue = false
        )
        {
            Debug.Log("[TextToSpeechNoOpService] Speak ignored");
            return false;
        }

        public void Stop() { }

        public bool IsSpeaking() => false;

        public string? ListVoicesJson() => null;

        public AudioClip? SynthesizeToClip(
            string text,
            float rate01 = 1.0f,
            float pitch = 1.0f,
            float volume01 = 1.0f
        ) => null;
    }
}
