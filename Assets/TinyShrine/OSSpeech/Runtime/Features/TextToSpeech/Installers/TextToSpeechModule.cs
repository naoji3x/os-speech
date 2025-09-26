using VContainer;
using VContainer.Unity;

namespace TinyShrine.OSSpeech.TextToSpeech.Installers
{
    /// <summary>
    /// TextToSpeech の登録処理をモジュールとして切り出したもの。
    /// これを呼べばプラットフォーム別 Contributor と AutoInitializer まで含めて登録されます。
    /// </summary>
    public static class TextToSpeechModule
    {
        public static void Register(IContainerBuilder builder)
        {
            // Fallback（No-Op）
            builder.Register<ITextToSpeechService, TextToSpeechStubService>(Lifetime.Singleton);

            // プラットフォーム別 Contributor を適用（あれば上書き登録）
            if (TextToSpeechRegistrationRegistry.TryRegister(builder))
            {
                UnityEngine.Debug.Log("[TextToSpeechModule] Platform-specific TextToSpeech service registered.");
            }
            else
            {
                UnityEngine.Debug.Log("[TextToSpeechModule] No platform-specific contributor. Using NoOp.");
            }

            // 自動初期化
            builder.RegisterEntryPoint<TextToSpeechAutoInitializer>(Lifetime.Singleton);
        }
    }
}
