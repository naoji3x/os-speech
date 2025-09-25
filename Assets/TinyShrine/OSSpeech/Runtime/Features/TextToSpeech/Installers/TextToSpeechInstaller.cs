using VContainer;
using VContainer.Unity;

namespace TinyShrine.OSSpeech.TextToSpeech.Installers
{
    /// <summary>
    /// TextToSpeech サービスの VContainer インストーラー。
    /// プラットフォームに応じて適切な実装を自動選択し、Singleton として登録します。
    /// </summary>
    public class TextToSpeechInstaller : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            RegisterTextToSpeechService(builder);
            builder.RegisterEntryPoint<TextToSpeechAutoInitializer>(Lifetime.Singleton);
            UnityEngine.Debug.Log("[TextToSpeechInstaller] Registered TextToSpeechAutoInitializer");
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
    }
}
