using TinyShrine.OSSpeech.SpeechToText.Installers;
using VContainer;

namespace TinyShrine.OSSpeech.SpeechToText
{
    public sealed class SpeechToTextAppleRegistrationContributor : ISpeechToTextRegistrationContributor
    {
        public void Register(IContainerBuilder builder)
        {
#if UNITY_IOS || UNITY_STANDALONE_OSX
            UnityEngine.Debug.Log("[AppleContributor] Registering SpeechToTextAppleService");
            builder.Register<ISpeechToTextService, SpeechToTextAppleService>(Lifetime.Singleton);
#endif
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSelf() =>
            SpeechToTextRegistrationRegistry.Add(new SpeechToTextAppleRegistrationContributor());
    }
}
