using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YgCore.Contracts;

namespace YgCore.Mocks
{
    /// <summary>
    /// Мок покупок. Каталог товаров игра задаёт сама (конструктор с каталогом или список <see cref="Catalog"/>),
    /// без этого используется небольшой общий набор. Невыданные покупки добавляются через <see cref="AddPendingPurchase"/>.
    /// </summary>
    public sealed class EditorMockPurchasesService : EditorMockPlatformService, IPurchasesService
    {
        // Адрес значка валюты совпадает с примером из PluginYG2 (Payments_info).
        // В WebGL настоящий адрес всегда приходит из каталога платформы.
        private const string MockCurrencyImageUrl =
            "https://yastatic.net/s3/games-static/static-data/images/payments/sdk/currency-icon-s@2x.png";

        private readonly List<PurchaseProduct> catalog = new List<PurchaseProduct>();
        private readonly List<PurchaseReceipt> restoredPurchases = new List<PurchaseReceipt>();
        private readonly HashSet<string> consumedTokens = new HashSet<string>();

        /// <summary>Мок с общим тестовым каталогом: три набора монет и один постоянный товар.</summary>
        public EditorMockPurchasesService()
        {
            catalog.Add(new PurchaseProduct("coins_small", "Small coin pack", "49", "YAN", true, MockCurrencyImageUrl));
            catalog.Add(new PurchaseProduct("coins_medium", "Medium coin pack", "99", "YAN", true, MockCurrencyImageUrl));
            catalog.Add(new PurchaseProduct("coins_large", "Large coin pack", "199", "YAN", true, MockCurrencyImageUrl));
            catalog.Add(new PurchaseProduct("premium_item", "Premium item", "299", "YAN", false, MockCurrencyImageUrl));
        }

        /// <summary>Мок с каталогом игры: общий набор не добавляется.</summary>
        public EditorMockPurchasesService(IEnumerable<PurchaseProduct> products)
        {
            if (products == null)
            {
                throw new ArgumentNullException(nameof(products));
            }

            catalog.AddRange(products);
        }

        /// <summary>Если включено, все покупки получают один и тот же токен (проверка защиты от дублей).</summary>
        public bool ReturnDuplicateToken { get; set; }

        public List<PurchaseProduct> Catalog => catalog;

        /// <summary>Невыданные покупки, которые вернёт <see cref="RestorePurchasesAsync"/>.</summary>
        public List<PurchaseReceipt> RestoredPurchases => restoredPurchases;

        public Task<PlatformServiceResult<List<PurchaseProduct>>> GetCatalogAsync()
        {
            return Task.FromResult(BuildResult(catalog));
        }

        public Task<PlatformServiceResult<PurchaseReceipt>> PurchaseAsync(string productId)
        {
            var token = ReturnDuplicateToken ? "mock-duplicate-token" : $"mock-{productId}-{Guid.NewGuid():N}";
            var receipt = new PurchaseReceipt(productId, token, false, DateTime.UtcNow);
            var result = BuildResult(receipt, "Мок-покупка выполнена.");
            return Task.FromResult(result);
        }

        public Task<PlatformServiceResult<List<PurchaseReceipt>>> RestorePurchasesAsync()
        {
            return Task.FromResult(BuildResult(restoredPurchases));
        }

        public Task<PlatformServiceResult> ConsumeAsync(string purchaseToken)
        {
            var result = BuildResult("Мок-покупка подтверждена.");
            if (result.IsSuccess)
            {
                consumedTokens.Add(purchaseToken);
            }

            return Task.FromResult(result);
        }

        /// <summary>Добавляет невыданную покупку, как будто игра закрылась между оплатой и выдачей награды.</summary>
        public void AddPendingPurchase(string productId, string token)
        {
            restoredPurchases.Add(new PurchaseReceipt(productId, token, false, DateTime.UtcNow));
        }

        /// <summary>Подтверждалась ли выдача покупки с таким токеном.</summary>
        public bool WasConsumed(string purchaseToken)
        {
            return consumedTokens.Contains(purchaseToken);
        }
    }
}
