using System;
using YgCore.Contracts;
using YgCore.Localization;

namespace YgCore.Factories
{
    /// <summary>
    /// Выбирает реализацию сервиса локализации.
    /// В редакторе возвращает мок, в WebGL с модулем Localization_yg реализацию на PluginYG2, иначе заглушку «недоступно».
    /// </summary>
    public static class LocalizationServiceFactory
    {
        /// <param name="catalog">Каталог строк игры. Нужен всем реализациям.</param>
        /// <param name="editorService">Свой мок для редактора. Если не задан, создаётся мок с английским языком.</param>
        public static ILocalizationService Create(
            ILocalizationCatalog catalog,
            ILocalizationService editorService = null)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

#if UNITY_EDITOR
            return editorService ?? new Mocks.EditorMockLocalizationService(catalog);
#elif UNITY_WEBGL && Localization_yg
            return new Yandex.PluginYG2LocalizationService(catalog);
#else
            return new UnavailableLocalizationService(catalog);
#endif
        }
    }
}
