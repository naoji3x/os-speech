using TinyShrine.OSSpeech.SpeechToText.Installers;
using TinyShrine.OSSpeech.TextToSpeech.Installers;
using VContainer;
using VContainer.Unity;

namespace TinyShrine.OSSpeech.Installers
{
    public class OSSpeechInstaller : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // 各機能モジュールの登録を順に適用（依存は *.Installers に集約）
            SpeechToTextModule.Register(builder);
            TextToSpeechModule.Register(builder);
        }
    }
}
