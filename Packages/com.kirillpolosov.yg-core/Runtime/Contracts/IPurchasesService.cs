using System.Collections.Generic;
using System.Threading.Tasks;

namespace YgCore.Contracts
{
    /// <summary>
    /// Покупки за реальные деньги. Схема в два шага: сначала <see cref="PurchaseAsync"/>, затем игра
    /// выдаёт награду и сохраняет её, и только после этого вызывает <see cref="ConsumeAsync"/>.
    /// Так покупка не пропадёт, если игра закрылась между оплатой и выдачей: при следующем запуске
    /// её вернёт <see cref="RestorePurchasesAsync"/>.
    /// </summary>
    public interface IPurchasesService
    {
        bool IsPlatformAvailable { get; }

        /// <summary>Каталог товаров, заведённых в консоли платформы.</summary>
        Task<PlatformServiceResult<List<PurchaseProduct>>> GetCatalogAsync();

        /// <summary>Покупает товар. Denied: покупка не состоялась (игрок отменил оплату или платформа вернула отказ),
        /// в сообщении результата причина, если платформа её назвала.</summary>
        Task<PlatformServiceResult<PurchaseReceipt>> PurchaseAsync(string productId);

        /// <summary>Возвращает покупки, выдача которых ещё не подтверждена (невыданные).</summary>
        Task<PlatformServiceResult<List<PurchaseReceipt>>> RestorePurchasesAsync();

        /// <summary>Подтверждает выдачу покупки по токену. Вызывать только после выдачи и сохранения награды.</summary>
        Task<PlatformServiceResult> ConsumeAsync(string purchaseToken);
    }
}
