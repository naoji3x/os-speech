using TinyShrine.OSSpeech.TextToSpeech.Installers;
using VContainer;

namespace TinyShrine.OSSpeech.TextToSpeech
{
    public sealed class TextToSpeechAppleRegistrationContributor : ITextToSpeechRegistrationContributor
    {
        public void Register(IContainerBuilder builder)
        {
#if UNITY_IOS || UNITY_STANDALONE_OSX
            UnityEngine.Debug.Log("[TTS][AppleContributor] Registering TextToSpeechAppleService");
            builder.Register<ITextToSpeechService, TextToSpeechAppleService>(Lifetime.Singleton);
#endif
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSelf() =>
            TextToSpeech.Installers.TextToSpeechRegistrationRegistry.Add(
                new TextToSpeechAppleRegistrationContributor()
            );
    }
}
