using TinyShrine.OSSpeech.SpeechToText.Installers;
using UnityEngine;

namespace TinyShrine.OSSpeech.SpeechToText
{
    public sealed class SpeechToTextAndroidRegistrationContributor : ISpeechToTextRegistrationContributor
    {
        public void Register(VContainer.IContainerBuilder builder)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            UnityEngine.Debug.Log("[AndroidContributor] Registering SpeechToTextAndroidService");
            builder.Register<ISpeechToTextService, SpeechToTextAndroidService>(VContainer.Lifetime.Singleton);
#endif
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSelf() =>
            SpeechToTextRegistrationRegistry.Add(new SpeechToTextAndroidRegistrationContributor());
    }
}
