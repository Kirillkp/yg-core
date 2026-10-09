namespace YgCore.Contracts
{
    /// <summary>Товар из каталога платформы. Неизменяемый.</summary>
    public sealed class PurchaseProduct
    {
        public PurchaseProduct(string id, string title, string price, string currencyCode, bool isConsumable,
            string currencyImageUrl = null)
        {
            Id = id;
            Title = title;
            Price = price;
            CurrencyCode = currencyCode;
            IsConsumable = isConsumable;
            CurrencyImageUrl = currencyImageUrl;
        }

        /// <summary>Идентификатор товара, заданный в консоли платформы.</summary>
        public string Id { get; }

        public string Title { get; }

        /// <summary>Цена так, как её вернула платформа (может уже содержать валюту).</summary>
        public string Price { get; }

        public string CurrencyCode { get; }

        /// <summary>Адрес значка валюты платформы, если есть.</summary>
        public string CurrencyImageUrl { get; }

        /// <summary>
        /// Цена для показа игроку. Добавляет код валюты, только если его ещё нет в строке цены,
        /// чтобы не получилось «100 YAN YAN».
        /// </summary>
        public string DisplayPrice
        {
            get
            {
                var price = Price?.Trim() ?? string.Empty;
                var currency = CurrencyCode?.Trim() ?? string.Empty;
                if (currency.Length == 0 || price.EndsWith(currency, System.StringComparison.OrdinalIgnoreCase))
                {
                    return price;
                }

                return price.Length == 0 ? currency : price + " " + currency;
            }
        }

        /// <summary>Расходуемый ли товар (покупается много раз, например валюта) или постоянный.</summary>
        public bool IsConsumable { get; }
    }
}
