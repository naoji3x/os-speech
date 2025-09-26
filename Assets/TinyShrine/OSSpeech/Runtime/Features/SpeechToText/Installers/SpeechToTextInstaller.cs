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
            // モジュールに委譲
            SpeechToTextModule.Register(builder);
        }
    }
}
