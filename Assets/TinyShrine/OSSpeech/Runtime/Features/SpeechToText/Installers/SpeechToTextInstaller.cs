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
#if UNITY_IOS || UNITY_STANDALONE_OSX
            // Apple プラットフォーム (iOS/macOS)
            UnityEngine.Debug.Log("[SpeechToTextInstaller] Registering SpeechToTextAppleService");
            builder.Register<ISpeechToTextService, SpeechToTextAppleService>(Lifetime.Singleton);

#elif UNITY_ANDROID && !UNITY_EDITOR
            // Android プラットフォーム
            UnityEngine.Debug.Log("[SpeechToTextInstaller] Registering SpeechToTextAndroidService");
            builder.Register<ISpeechToTextService, SpeechToTextAndroidService>(Lifetime.Singleton);
#else
            // その他のプラットフォーム (Editor/Windows/Linux等) ※Editorでは権限問題で動しないためStubを登録
            UnityEngine.Debug.Log("[SpeechToTextInstaller] Registering SpeechToTextStubService (Stub implementation)");
            builder.Register<ISpeechToTextService, SpeechToTextStubService>(Lifetime.Singleton);
#endif
        }
    }
}
