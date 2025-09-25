using TinyShrine.OSSpeech.SpeechToText;
using TinyShrine.OSSpeech.TextToSpeech;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TinyShrine.OSSpeech.Sample
{
    /// <summary>
    /// Sample script for OSSpeech
    /// </summary>
    public class OSSpeechSample : MonoBehaviour
    {
        [Inject]
        private readonly ISpeechToTextService? speechToTextService;

        [Inject]
        private readonly ITextToSpeechService? textToSpeechService;

        [SerializeField]
        private TMP_InputField field;

        [SerializeField]
        private Image buttonImage;
        private bool isRecording;

        [SerializeField]
        private string text = "こんにちは、音声合成のテストです。";

        public void OnMicClick()
        {
            if (isRecording)
            {
                if (!string.IsNullOrWhiteSpace(field.text))
                {
                    this.text = field.text;
                }
                speechToTextService?.Stop();
                buttonImage.color = Color.white;
            }
            else
            {
                this.text = string.Empty;
                speechToTextService?.Start();
                buttonImage.color = Color.red;
            }
            isRecording = !isRecording;
        }

        public void OnSpeakClick()
        {
            textToSpeechService?.Speak(this.text);
        }

        private void OnPartial(string text)
        {
            this.text = text;
            field.text = this.text;
        }

        private void OnFinal(string text)
        {
            Debug.Log($"Final result: {text}");
            this.text = text;
            field.text = this.text;
        }

        private void Start()
        {
            if (speechToTextService == null)
            {
                Debug.LogError(
                    "[SpeechToTextService] ISpeechToTextService not injected. Make sure SpeechToTextInstaller is in the scene."
                );
                return;
            }
            Debug.Log("OSSpeechSample Awake: Starting initialization...");
            speechToTextService.OnPartial += OnPartial;
            speechToTextService.OnFinal += OnFinal;
            Debug.Log("OSSpeechSample Awake: Initialization complete.");
        }

        private void OnDestroy()
        {
            if (speechToTextService != null)
            {
                speechToTextService.OnPartial -= OnPartial;
                speechToTextService.OnFinal -= OnFinal;
            }
        }
    }
}
