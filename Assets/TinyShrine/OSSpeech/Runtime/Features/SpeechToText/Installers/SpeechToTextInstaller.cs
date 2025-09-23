using VContainer;
using VContainer.Unity;

namespace TinyShrine.OSSpeech.SpeechToText.Installers
{
    /// <summary>
    /// 音声認識サービスのDI設定を行うVContainerインストーラー。
    /// プラットフォームに応じて適切な実装を自動選択し、Singletonとして登録します。
    /// </summary>
    public class SpeechToTextInstaller : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // プラットフォーム別の実装を登録
            RegisterSpeechToTextService(builder);

            // 自動初期化サービスを登録
            builder.RegisterEntryPoint<SpeechToTextAutoInitializer>(Lifetime.Singleton);
            UnityEngine.Debug.Log("[SpeechToTextInstaller] Registered SpeechToTextAutoInitializer");
        }

        /// <summary>
        /// プラットフォームに応じてISpeechToTextServiceの実装を登録します。
        /// </summary>
        /// <param name="builder">VContainerのコンテナビルダー</param>
        private void RegisterSpeechToTextService(IContainerBuilder builder)
        {
            // まずデフォルト（Stub）
            UnityEngine.Debug.Log("[SpeechToTextInstaller] Registering SpeechToTextStubService (default)");
            builder.Register<ISpeechToTextService, SpeechToTextStubService>(Lifetime.Singleton);

            // プラットフォームのコントリビュータがあればそれを採用（既存登録を上書き）
            if (SpeechToTextRegistrationRegistry.TryRegister(builder))
            {
                UnityEngine.Debug.Log("[SpeechToTextInstaller] Platform-specific SpeechToText service registered.");
            }
            else
            {
                UnityEngine.Debug.Log("[SpeechToTextInstaller] No platform-specific contributor. Using Stub.");
            }
        }
    }
}
