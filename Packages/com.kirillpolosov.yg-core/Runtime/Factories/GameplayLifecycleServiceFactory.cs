using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Выбирает реализацию сервиса: жизненный цикл геймплея.
    /// В редакторе возвращает мок, в WebGL с модулем YandexGamesPlatform_yg реализацию на PluginYG2, иначе заглушку «недоступно».
    /// </summary>
    public static class GameplayLifecycleServiceFactory
    {
        /// <param name="editorService">Свой мок для редактора (например, с нужным сценарием). Если не задан, создаётся мок по умолчанию.</param>
        public static IGameplayLifecycleService Create(IGameplayLifecycleService editorService = null)
        {
#if UNITY_EDITOR
            return editorService ?? new Mocks.EditorMockGameplayLifecycleService();
#elif UNITY_WEBGL && YandexGamesPlatform_yg
            return new Yandex.PluginYG2GameplayLifecycleService();
#else
            return new UnavailableGameplayLifecycleService();
#endif
        }
    }
}
