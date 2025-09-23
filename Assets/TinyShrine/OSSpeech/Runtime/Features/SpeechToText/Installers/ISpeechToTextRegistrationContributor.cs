namespace TinyShrine.OSSpeech.SpeechToText.Installers
{
    /// <summary>
    /// プラットフォーム固有の ISpeechToTextService を VContainer に登録する拡張ポイント。
    /// </summary>
    public interface ISpeechToTextRegistrationContributor
    {
        /// <summary>
        /// Registers the platform-specific <c>ISpeechToTextService</c> implementation into the VContainer DI container.
        /// </summary>
        /// <param name="builder">The VContainer <c>IContainerBuilder</c> used for dependency registration.</param>
        void Register(VContainer.IContainerBuilder builder);
    }
}
