using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Выбирает реализацию сервиса: ярлык игры.
    /// В редакторе возвращает мок, в WebGL на Яндекс Играх реализацию на SDK платформы, иначе заглушку «недоступно».
    /// </summary>
    public static class ShortcutServiceFactory
    {
        /// <param name="editorService">Свой мок для редактора (например, с нужным сценарием). Если не задан, создаётся мок по умолчанию.</param>
        public static IShortcutService Create(IShortcutService editorService = null)
        {
#if UNITY_EDITOR
            return editorService ?? new Mocks.EditorMockShortcutService();
#elif UNITY_WEBGL && YandexGamesPlatform_yg
            return new Yandex.PluginYG2ShortcutService();
#else
            return new UnavailableShortcutService();
#endif
        }
    }
}
