using System;
using System.Collections.Generic;

namespace TinyShrine.OSSpeech.SpeechToText.Installers
{
    /// <summary>
    /// プラットフォーム固有の登録を集約するレジストリ。
    /// 同一プラットフォームで複数あっても先着1件を採用。
    /// </summary>
    public static class SpeechToTextRegistrationRegistry
    {
        private static readonly List<ISpeechToTextRegistrationContributor> Contributors = new();

        public static void Add(ISpeechToTextRegistrationContributor contributor)
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
