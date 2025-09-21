using TinyShrine.OSSpeech.SpeechToText;
using TinyShrine.OSSpeech.TextToSpeech;
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
                SpeechToTextService.Stop();
                buttonImage.color = Color.white;
            }
            else
            {
                SpeechToTextService.Start();
                buttonImage.color = Color.red;
            }
            isRecording = !isRecording;
        }

        public void OnSpeakClick()
        {
            TextToSpeechService.Speak(this.text);
        }

        private void SetText(string text) => field.text = text;

        private void Awake()
        {
            Debug.Log("OSSpeechSample Awake");
            // メインスレッドの SynchronizationContext を渡す（ここがポイント）
            SpeechToTextService.Init(locale: "ja-JP", mainContext: System.Threading.SynchronizationContext.Current);
            SpeechToTextService.OnPartial += SetText;
            SpeechToTextService.OnFinal += SetText;

            TextToSpeechService.Init(System.Threading.SynchronizationContext.Current, language: "ja-JP");
        }

        private void OnDestroy()
        {
            SpeechToTextService.OnPartial -= SetText;
            SpeechToTextService.OnFinal -= SetText;
            SpeechToTextService.Stop();
        }
    }
}
