using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Выбирает реализацию сервиса: реклама.
    /// В редакторе возвращает мок, в WebGL с модулем RewardedAdv_yg или InterstitialAdv_yg реализацию на PluginYG2, иначе заглушку «недоступно».
    /// </summary>
    public static class AdsServiceFactory
    {
        /// <param name="editorService">Свой мок для редактора (например, с нужным сценарием). Если не задан, создаётся мок по умолчанию.</param>
        public static IAdsService Create(IAdsService editorService = null)
        {
#if UNITY_EDITOR
            return editorService ?? new Mocks.EditorMockAdsService();
#elif UNITY_WEBGL && (RewardedAdv_yg || InterstitialAdv_yg)
            return new Yandex.PluginYG2AdsService();
#else
            return new UnavailableAdsService();
#endif
        }
    }
}
