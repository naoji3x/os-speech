using TinyShrine.OSSpeech.SpeechToText;
// using TinyShrine.OSSpeech.TextToSpeech;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TinyShrine.OSSpeech.Sample
{
    /// <summary>
    /// Sample script for OSSpeech
    /// </summary>
    public class OSSpeechSample : MonoBehaviour
    {
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
                SpeechToTextAndroidService.Stop();
                buttonImage.color = Color.white;
            }
            else
            {
                this.text = string.Empty;
                SpeechToTextAndroidService.Start();
                buttonImage.color = Color.red;
            }
            isRecording = !isRecording;
        }

        public void OnSpeakClick()
        {
            // TextToSpeechService.Speak(this.text);
        }

        private void OnPartial(string text)
        {
            this.text = text;
            field.text = this.text;
            Debug.Log($"Partial result: {text}");
        }

        private void OnFinal(string text)
        {
            this.text = text;
            field.text = this.text;
            Debug.Log($"Final result: {text}");
        }

        private void OnStateChanged(string state) => Debug.Log($"State: {state}");

        private void Awake()
        {
            Debug.Log("OSSpeechSample Awake: Starting initialization...");
            // メインスレッドの SynchronizationContext を渡す（ここがポイント）
            SpeechToTextAndroidService.Init(
                locale: "ja-JP",
                mainContext: System.Threading.SynchronizationContext.Current
            );
            SpeechToTextAndroidService.OnPartial += OnPartial;
            SpeechToTextAndroidService.OnFinal += OnFinal;
            SpeechToTextAndroidService.OnStateChanged += OnStateChanged;

            Debug.Log("OSSpeechSample Awake: Initialization complete.");

            // TextToSpeechService.Init(System.Threading.SynchronizationContext.Current, language: "ja-JP");
        }

        private void OnDestroy()
        {
            SpeechToTextAndroidService.OnPartial -= OnPartial;
            SpeechToTextAndroidService.OnFinal -= OnFinal;
            SpeechToTextAndroidService.OnStateChanged -= OnStateChanged;
        }
    }
}
