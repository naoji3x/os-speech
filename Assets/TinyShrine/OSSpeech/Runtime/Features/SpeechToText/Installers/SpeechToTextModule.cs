using VContainer;
using VContainer.Unity;

namespace TinyShrine.OSSpeech.SpeechToText.Installers
{
    /// <summary>
    /// SpeechToText の登録処理をモジュール化。
    /// </summary>
    public static class SpeechToTextModule
    {
        public static void Register(IContainerBuilder builder)
        {
            // Fallback（Stub）
            builder.Register<ISpeechToTextService, SpeechToTextStubService>(Lifetime.Singleton);

            // プラットフォーム別 Contributor を適用
            if (SpeechToTextRegistrationRegistry.TryRegister(builder))
            {
                UnityEngine.Debug.Log("[SpeechToTextModule] Platform-specific SpeechToText service registered.");
            }
            else
            {
                UnityEngine.Debug.Log("[SpeechToTextModule] No platform-specific contributor. Using Stub.");
            }

            // 自動初期化
            builder.RegisterEntryPoint<SpeechToTextAutoInitializer>(Lifetime.Singleton);
        }
    }
}
