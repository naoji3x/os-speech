using TinyShrine.OSSpeech.TextToSpeech.Installers;
using VContainer;

namespace TinyShrine.OSSpeech.TextToSpeech
{
    public sealed class TextToSpeechAndroidRegistrationContributor : ITextToSpeechRegistrationContributor
    {
        public void Register(IContainerBuilder builder)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            UnityEngine.Debug.Log("[TTS][AndroidContributor] Registering TextToSpeechAndroidService");
            builder.Register<ITextToSpeechService, TextToSpeechAndroidService>(Lifetime.Singleton);
#endif
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSelf() =>
            TextToSpeech.Installers.TextToSpeechRegistrationRegistry.Add(
                new TextToSpeechAndroidRegistrationContributor()
            );
    }
}
