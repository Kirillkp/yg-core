using System.Collections.Generic;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Factories
{
    /// <summary>Заглушка покупок без платформы: каталога нет, купить ничего нельзя.</summary>
    public sealed class UnavailablePurchasesService : IPurchasesService
    {
        private const string Message = "Покупки на платформе недоступны.";

        public bool IsPlatformAvailable => false;

        public Task<PlatformServiceResult<List<PurchaseProduct>>> GetCatalogAsync()
        {
            return Task.FromResult(
                PlatformServiceResult<List<PurchaseProduct>>.Unavailable(Message));
        }

        public Task<PlatformServiceResult<PurchaseReceipt>> PurchaseAsync(string productId)
        {
            return Task.FromResult(
                PlatformServiceResult<PurchaseReceipt>.Unavailable(Message));
        }

        public Task<PlatformServiceResult<List<PurchaseReceipt>>> RestorePurchasesAsync()
        {
            return Task.FromResult(
                PlatformServiceResult<List<PurchaseReceipt>>.Unavailable(Message));
        }

        public Task<PlatformServiceResult> ConsumeAsync(string purchaseToken)
        {
            return Task.FromResult(PlatformServiceResult.Unavailable(Message));
        }
    }
}
