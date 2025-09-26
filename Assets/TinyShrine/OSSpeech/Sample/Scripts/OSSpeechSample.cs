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
        private const string Text = "こんにちは、音声合成のテストです。";

        [Inject]
        private readonly ISpeechToTextService? speechToTextService;

        [Inject]
        private readonly ITextToSpeechService? textToSpeechService;

        [SerializeField]
        private TMP_InputField field;

        [SerializeField]
        private Image buttonImage;
        private bool isRecording;

        public void OnMicClick()
        {
            Debug.Log("OnMicClick" + (isRecording ? " Stop" : " Start"));
            if (isRecording)
            {
                MicOff();
            }
            else
            {
                MicOn();
            }
        }

        public void OnSpeakClick()
        {
            Debug.Log($"OnSpeakClick: {field.text}");
            MicOff();
            textToSpeechService?.Speak(field.text);
        }

        private void MicOn()
        {
            if (isRecording || speechToTextService == null)
            {
                return;
            }
            speechToTextService?.Start();
            buttonImage.color = Color.red;
            isRecording = true;
        }

        private void MicOff()
        {
            if (!isRecording || speechToTextService == null)
            {
                return;
            }
            speechToTextService?.Stop();
            buttonImage.color = Color.white;
            isRecording = false;
        }

        private void OnPartial(string text)
        {
            Debug.Log($"Partial result: {text}");
            field.text = text;
        }

        private void OnFinal(string text)
        {
            Debug.Log($"Final result: {text}");
            field.text = text;
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
            if (textToSpeechService == null)
            {
                Debug.LogError(
                    "[TextToSpeechService] ITextToSpeechService not injected. Make sure TextToSpeechInstaller is in the scene."
                );
                return;
            }
            field.text = Text;
            speechToTextService.OnPartial += OnPartial;
            speechToTextService.OnFinal += OnFinal;
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
