using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Выбирает реализацию сервиса: оценка игры.
    /// В редакторе возвращает мок, в WebGL на Яндекс Играх реализацию на SDK платформы, иначе заглушку «недоступно».
    /// </summary>
    public static class ReviewServiceFactory
    {
        /// <param name="editorService">Свой мок для редактора (например, с нужным сценарием). Если не задан, создаётся мок по умолчанию.</param>
        public static IReviewService Create(IReviewService editorService = null)
        {
#if UNITY_EDITOR
            return editorService ?? new Mocks.EditorMockReviewService();
#elif UNITY_WEBGL && YandexGamesPlatform_yg
            return new Yandex.PluginYG2ReviewService();
#else
            return new UnavailableReviewService();
#endif
        }
    }
}
