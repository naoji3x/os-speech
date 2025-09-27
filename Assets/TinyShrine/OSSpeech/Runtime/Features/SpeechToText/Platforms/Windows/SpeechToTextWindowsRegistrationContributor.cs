using TinyShrine.OSSpeech.SpeechToText.Installers;
using VContainer;

namespace TinyShrine.OSSpeech.SpeechToText
{
    public sealed class SpeechToTextWindowsRegistrationContributor : ISpeechToTextRegistrationContributor
    {
        public void Register(IContainerBuilder builder)
        {
#if UNITY_STANDALONE_WIN || UNITY_WSA
            UnityEngine.Debug.Log("[WindowsContributor] Registering SpeechToTextWindowsService");
            builder.Register<ISpeechToTextService, SpeechToTextWindowsService>(Lifetime.Singleton);
#endif
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSelf()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_WSA
            SpeechToTextRegistrationRegistry.Add(new SpeechToTextWindowsRegistrationContributor());
#endif
        }
    }
}
