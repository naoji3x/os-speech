using System;
using System.Collections.Generic;

namespace TinyShrine.OSSpeech.TextToSpeech.Installers
{
    /// <summary>
    /// プラットフォーム固有の TextToSpeech 登録コントリビュータを集約するレジストリ。
    /// 同一プラットフォームで複数あっても先着1件を採用。
    /// </summary>
    public static class TextToSpeechRegistrationRegistry
    {
        private static readonly List<ITextToSpeechRegistrationContributor> Contributors = new();

        public static void Add(ITextToSpeechRegistrationContributor contributor)
        {
            if (contributor == null)
            {
                throw new ArgumentNullException(nameof(contributor));
            }
            Contributors.Add(contributor);
        }

        public static bool TryRegister(VContainer.IContainerBuilder builder)
        {
            foreach (var c in Contributors)
            {
                c.Register(builder);
                return true;
            }
            return false;
        }
    }
}
