using System;
using System.Threading.Tasks;
using YgCore.Contracts;
using YgCore.Localization;

namespace YgCore.Mocks
{
    /// <summary>
    /// Мок локализации: язык задаётся вручную, строки берутся из каталога игры.
    /// </summary>
    public sealed class EditorMockLocalizationService : EditorMockPlatformService, ILocalizationService
    {
        private readonly ILocalizationCatalog catalog;

        public event EventHandler<LanguageChangedEventArgs> LanguageChanged;

        /// <param name="catalog">Каталог строк игры.</param>
        /// <param name="initialLanguage">Начальный язык. Если каталог его не поддерживает, берётся язык по умолчанию.</param>
        public EditorMockLocalizationService(ILocalizationCatalog catalog, string initialLanguage = "en")
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            CurrentLanguage = catalog.NormalizeLanguage(initialLanguage);
        }

        public string CurrentLanguage { get; private set; }

        public Task<PlatformServiceResult<string>> DetectLanguageAsync()
        {
            return Task.FromResult(BuildResult<string>(CurrentLanguage));
        }

        public void SetLanguage(string language)
        {
            var normalizedLanguage = catalog.NormalizeLanguage(language);
            if (normalizedLanguage == CurrentLanguage)
            {
                return;
            }

            var previousLanguage = CurrentLanguage;
            CurrentLanguage = normalizedLanguage;
            LanguageChanged?.Invoke(this, new LanguageChangedEventArgs(previousLanguage, CurrentLanguage));
        }

        public string Get(string key, params object[] arguments)
        {
            return catalog.Get(CurrentLanguage, key, arguments);
        }
    }
}
