using System;

namespace YgCore.Contracts
{
    /// <summary>Данные события смены языка.</summary>
    public sealed class LanguageChangedEventArgs : EventArgs
    {
        public LanguageChangedEventArgs(string previousLanguage, string currentLanguage)
        {
            PreviousLanguage = previousLanguage;
            CurrentLanguage = currentLanguage;
        }

        /// <summary>Язык до смены.</summary>
        public string PreviousLanguage { get; }

        /// <summary>Язык после смены.</summary>
        public string CurrentLanguage { get; }
    }
}
