using System;
using System.Threading.Tasks;
using YgCore.Contracts;
using YgCore.Localization;

namespace YgCore.Factories
{
    /// <summary>
    /// Заглушка локализации без платформы: язык определить нельзя, но строки игры работают
    /// и язык можно переключить вручную. Начальный язык: язык по умолчанию из каталога.
    /// </summary>
    public sealed class UnavailableLocalizationService : ILocalizationService
    {
        private readonly ILocalizationCatalog catalog;

        public UnavailableLocalizationService(ILocalizationCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            CurrentLanguage = catalog.DefaultLanguage;
        }

        public event EventHandler<LanguageChangedEventArgs> LanguageChanged;

        public bool IsPlatformAvailable => false;

        public string CurrentLanguage { get; private set; }

        public Task<PlatformServiceResult<string>> DetectLanguageAsync()
        {
            return Task.FromResult(
                PlatformServiceResult<string>.Unavailable(
                    "Язык платформы недоступен."));
        }

        public string Get(string key, params object[] arguments)
        {
            return catalog.Get(CurrentLanguage, key, arguments);
        }

        public void SetLanguage(string language)
        {
            var normalized = catalog.NormalizeLanguage(language);
            if (normalized == CurrentLanguage)
            {
                return;
            }

            var previous = CurrentLanguage;
            CurrentLanguage = normalized;
            LanguageChanged?.Invoke(
                this,
                new LanguageChangedEventArgs(previous, CurrentLanguage));
        }
    }
}
