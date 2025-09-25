namespace TinyShrine.OSSpeech.TextToSpeech.Installers
{
    /// <summary>
    /// プラットフォーム固有の ITextToSpeechService を VContainer に登録する拡張ポイント。
    /// </summary>
    public interface ITextToSpeechRegistrationContributor
    {
        /// <summary>
        /// Registers the platform-specific <c>ITextToSpeechService</c> implementation into the VContainer DI container.
        /// </summary>
        /// <param name="builder">The VContainer <c>IContainerBuilder</c> used for dependency registration.</param>
        void Register(VContainer.IContainerBuilder builder);
    }
}
