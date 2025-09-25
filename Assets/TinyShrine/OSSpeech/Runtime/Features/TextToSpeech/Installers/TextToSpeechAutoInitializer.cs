using System;
using System.Threading;
using UnityEngine;
using VContainer.Unity;

namespace TinyShrine.OSSpeech.TextToSpeech.Installers
{
    /// <summary>
    /// TextToSpeech サービスの自動初期化を行うクラス。
    /// </summary>
    public class TextToSpeechAutoInitializer : IStartable, IDisposable
    {
        private readonly ITextToSpeechService ttsService;
        private bool initialized;

        public TextToSpeechAutoInitializer(ITextToSpeechService ttsService)
        {
            this.ttsService = ttsService;
        }

        public void Start()
        {
            if (initialized)
            {
                return;
            }
            try
            {
                var mainContext = SynchronizationContext.Current;
                ttsService.Init(mainContext, "ja-JP");
                initialized = true;
                Debug.Log("[TextToSpeechAutoInitializer] TextToSpeech service initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[TextToSpeechAutoInitializer] Failed to initialize TextToSpeech service: {ex.Message}"
                );
            }
        }

        public void Dispose()
        {
            if (!initialized)
            {
                return;
            }
            try
            {
                ttsService.Stop();
                Debug.Log("[TextToSpeechAutoInitializer] TextToSpeech service disposed");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TextToSpeechAutoInitializer] Error during disposal: {ex.Message}");
            }
            finally
            {
                initialized = false;
                GC.SuppressFinalize(this);
            }
        }
    }
}
