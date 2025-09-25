using TinyShrine.OSSpeech.SpeechToText;
using TinyShrine.OSSpeech.SpeechToText.Installers;
using TinyShrine.OSSpeech.TextToSpeech;
using TinyShrine.OSSpeech.TextToSpeech.Installers;
using VContainer;
using VContainer.Unity;

namespace TinyShrine.OSSpeech.Installers
{
    public class OSSpeechInstaller : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // プラットフォーム別の実装を登録
            RegisterSpeechToTextService(builder);
            RegisterTextToSpeechService(builder);

            // 自動初期化サービスを登録
            builder.RegisterEntryPoint<SpeechToTextAutoInitializer>(Lifetime.Singleton);
            builder.RegisterEntryPoint<TextToSpeechAutoInitializer>(Lifetime.Singleton);
            UnityEngine.Debug.Log("[SpeechToTextInstaller] Registered SpeechToTextAutoInitializer");
        }

        private void RegisterTextToSpeechService(IContainerBuilder builder)
        {
            // まずデフォルト（何もせず警告だけ出すスタブを後で必要なら実装）
            UnityEngine.Debug.Log(
                "[TextToSpeechInstaller] Registering fallback TextToSpeechAppleService/TextToSpeechAndroidService expectation"
            );
            // デフォルトで Apple/Android 以外は何もしないため、簡易スタブをここに追加してもよい。
            // 現状はプラットフォーム contributor がなければエラーを避けるため NoOp 実装を登録
            builder.Register<ITextToSpeechService, TextToSpeechStubService>(Lifetime.Singleton);

            if (TextToSpeechRegistrationRegistry.TryRegister(builder))
            {
                UnityEngine.Debug.Log("[TextToSpeechInstaller] Platform-specific TextToSpeech service registered.");
            }
            else
            {
                UnityEngine.Debug.Log("[TextToSpeechInstaller] No platform-specific contributor. Using NoOp.");
            }
        }

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
