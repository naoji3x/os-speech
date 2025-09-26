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
            // モジュールに委譲
            TextToSpeechModule.Register(builder);
        }
    }
}
