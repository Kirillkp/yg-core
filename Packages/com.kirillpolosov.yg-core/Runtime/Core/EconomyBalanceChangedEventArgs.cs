using System;

namespace YgCore.Core
{
    /// <summary>Данные события изменения баланса.</summary>
    public sealed class EconomyBalanceChangedEventArgs : EventArgs
    {
        public EconomyBalanceChangedEventArgs(
            int previousBalance,
            int currentBalance,
            int amount,
            string transactionId,
            string reason)
        {
            PreviousBalance = previousBalance;
            CurrentBalance = currentBalance;
            Amount = amount;
            TransactionId = transactionId;
            Reason = reason;
        }

        /// <summary>Баланс до операции.</summary>
        public int PreviousBalance { get; }

        /// <summary>Баланс после операции.</summary>
        public int CurrentBalance { get; }

        /// <summary>Изменение: положительное при начислении, отрицательное при списании.</summary>
        public int Amount { get; }

        /// <summary>Идентификатор транзакции. Пустой при восстановлении баланса из сохранения.</summary>
        public string TransactionId { get; }

        /// <summary>Причина операции (произвольная строка для логов, например «coin_pickup»).</summary>
        public string Reason { get; }
    }
}
