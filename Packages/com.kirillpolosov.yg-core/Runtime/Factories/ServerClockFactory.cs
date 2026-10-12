using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Выбирает реализацию сервиса: серверное время.
    /// В редакторе возвращает мок, в WebGL на Яндекс Играх реализацию на SDK платформы, иначе заглушку «недоступно».
    /// </summary>
    public static class ServerClockFactory
    {
        /// <param name="editorService">Свой мок для редактора (например, с нужным сценарием). Если не задан, создаётся мок по умолчанию.</param>
        public static IServerClock Create(IServerClock editorService = null)
        {
#if UNITY_EDITOR
            return editorService ?? new Mocks.EditorMockServerClock();
#elif UNITY_WEBGL && YandexGamesPlatform_yg
            return new Yandex.PluginYG2ServerClock();
#else
            return new UnavailableServerClock();
#endif
        }
    }
}
