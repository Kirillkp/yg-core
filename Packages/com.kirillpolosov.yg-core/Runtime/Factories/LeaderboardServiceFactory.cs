using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Выбирает реализацию сервиса: таблицы лидеров.
    /// В редакторе возвращает мок, в WebGL с модулем Leaderboards_yg реализацию на PluginYG2, иначе заглушку «недоступно».
    /// </summary>
    public static class LeaderboardServiceFactory
    {
        /// <param name="editorService">Свой мок для редактора (например, с нужным сценарием). Если не задан, создаётся мок по умолчанию.</param>
        public static ILeaderboardService Create(ILeaderboardService editorService = null)
        {
#if UNITY_EDITOR
            return editorService ?? new Mocks.EditorMockLeaderboardService();
#elif UNITY_WEBGL && Leaderboards_yg
            return new Yandex.PluginYG2LeaderboardService();
#else
            return new UnavailableLeaderboardService();
#endif
        }
    }
}
