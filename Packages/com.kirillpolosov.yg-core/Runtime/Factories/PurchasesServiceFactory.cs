using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>
    /// Выбирает реализацию сервиса: покупки.
    /// В редакторе возвращает мок, в WebGL с модулем Payments_yg реализацию на PluginYG2, иначе заглушку «недоступно».
    /// </summary>
    public static class PurchasesServiceFactory
    {
        /// <param name="editorService">Свой мок для редактора (например, с нужным сценарием). Если не задан, создаётся мок по умолчанию.</param>
        public static IPurchasesService Create(IPurchasesService editorService = null)
        {
#if UNITY_EDITOR
            return editorService ?? new Mocks.EditorMockPurchasesService();
#elif UNITY_WEBGL && Payments_yg
            return new Yandex.PluginYG2PurchasesService();
#else
            return new UnavailablePurchasesService();
#endif
        }
    }
}
