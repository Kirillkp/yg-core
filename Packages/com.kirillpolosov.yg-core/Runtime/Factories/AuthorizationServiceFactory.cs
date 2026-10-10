using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Выбирает реализацию сервиса: авторизация.
    /// В редакторе возвращает мок, в WebGL с модулем Authorization_yg реализацию на PluginYG2, иначе заглушку «недоступно».
    /// </summary>
    public static class AuthorizationServiceFactory
    {
        /// <param name="editorService">Свой мок для редактора (например, с нужным сценарием). Если не задан, создаётся мок по умолчанию.</param>
        public static IAuthorizationService Create(IAuthorizationService editorService = null)
        {
#if UNITY_EDITOR
            return editorService ?? new Mocks.EditorMockAuthorizationService();
#elif UNITY_WEBGL && Authorization_yg
            return new Yandex.PluginYG2AuthorizationService();
#else
            return new UnavailableAuthorizationService();
#endif
        }
    }
}
