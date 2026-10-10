using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Выбирает реализацию сервиса: отметка готовности игры.
    /// В редакторе возвращает мок, в WebGL с модулем YandexGamesPlatform_yg реализацию на PluginYG2, иначе заглушку «недоступно».
    /// </summary>
    public static class GameReadinessServiceFactory
    {
        /// <param name="editorService">Свой мок для редактора (например, с нужным сценарием). Если не задан, создаётся мок по умолчанию.</param>
        public static IGameReadinessService Create(IGameReadinessService editorService = null)
        {
#if UNITY_EDITOR
            return editorService ?? new Mocks.EditorMockGameReadinessService();
#elif UNITY_WEBGL && YandexGamesPlatform_yg
            return new Yandex.PluginYG2GameReadinessService();
#else
            return new UnavailableGameReadinessService();
#endif
        }
    }
}
