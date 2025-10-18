using UnityEngine;

namespace TinyShrine.OSSpeech.TextToSpeech
{
    public class TtsDemo : MonoBehaviour
    {
        private AudioSource src;

        public void Start()
        {
            src = gameObject.AddComponent<AudioSource>();

            // voiceName に SAPIのボイス名（例："Microsoft Haruka Desktop"）を入れるとその声に
            TextToSpeechWindowsBridge.Init(voiceName: "Microsoft Haruka Desktop", rate: 0, volume: 100);

            var clip = TextToSpeechWindowsBridge.SpeakToClip(
                "こんにちは。ローカルTTSのテストです。UnityとSAPIで動いています。"
            );
            src.clip = clip;
            src.Play();
        }

        public void OnDestroy()
        {
            TextToSpeechWindowsBridge.Shutdown();
        }
    }
}
