using System;

namespace YgCore.Contracts
{
    /// <summary>Квитанция о покупке. Неизменяемая.</summary>
    public sealed class PurchaseReceipt
    {
        public PurchaseReceipt(string productId, string token, bool isConsumed, DateTime purchasedAt)
        {
            ProductId = productId;
            Token = token;
            IsConsumed = isConsumed;
            PurchasedAt = purchasedAt;
        }

        public string ProductId { get; }

        /// <summary>Токен покупки. Нужен, чтобы подтвердить выдачу (<see cref="IPurchasesService.ConsumeAsync"/>).</summary>
        public string Token { get; }

        /// <summary>Подтверждена ли выдача (погашена ли покупка на платформе).</summary>
        public bool IsConsumed { get; }

        public DateTime PurchasedAt { get; }
    }
}
