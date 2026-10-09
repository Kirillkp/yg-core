using System;
using System.Collections.Generic;

namespace YgCore.Core
{
    /// <summary>
    /// Реализация экономики в памяти на время сессии. Хранение между запусками
    /// выполняет слой сохранений: он вызывает <see cref="RestoreBalance"/> при загрузке.
    /// Журнал обработанных транзакций живёт только в этой сессии.
    /// </summary>
    public sealed class SessionEconomyService : IEconomyService
    {
        private readonly HashSet<string> processedTransactions = new HashSet<string>();

        public event EventHandler<EconomyBalanceChangedEventArgs> BalanceChanged;

        public int Balance { get; private set; }

        public bool TryCredit(string transactionId, int amount, string reason)
        {
            if (!CanProcess(transactionId, amount))
            {
                return false;
            }

            var previousBalance = Balance;

            // checked: при переполнении int лучше получить исключение, чем тихо уйти в минус.
            checked
            {
                Balance += amount;
            }

            CompleteTransaction(previousBalance, amount, transactionId, reason);
            return true;
        }

        public bool TrySpend(string transactionId, int amount, string reason)
        {
            if (!CanProcess(transactionId, amount) || amount > Balance)
            {
                return false;
            }

            var previousBalance = Balance;
            Balance -= amount;
            CompleteTransaction(previousBalance, -amount, transactionId, reason);
            return true;
        }

        public bool HasProcessed(string transactionId)
        {
            return !string.IsNullOrWhiteSpace(transactionId)
                && processedTransactions.Contains(transactionId);
        }

        /// <summary>
        /// Устанавливает баланс из сохранения. Это не транзакция: идентификатор пустой,
        /// причина «progress_restore». Отрицательные значения из повреждённых данных
        /// превращаются в ноль.
        /// </summary>
        public void RestoreBalance(int balance)
        {
            var restoredBalance = Math.Max(0, balance);
            if (Balance == restoredBalance)
            {
                return;
            }

            var previousBalance = Balance;
            Balance = restoredBalance;
            BalanceChanged?.Invoke(
                this,
                new EconomyBalanceChangedEventArgs(
                    previousBalance,
                    Balance,
                    Balance - previousBalance,
                    string.Empty,
                    "progress_restore"));
        }

        private bool CanProcess(string transactionId, int amount)
        {
            return !string.IsNullOrWhiteSpace(transactionId)
                && amount > 0
                && !processedTransactions.Contains(transactionId);
        }

        private void CompleteTransaction(
            int previousBalance,
            int amount,
            string transactionId,
            string reason)
        {
            // Идентификатор запоминаем до события: подписчик может повторить вызов,
            // и повтор уже должен быть отклонён.
            processedTransactions.Add(transactionId);
            BalanceChanged?.Invoke(
                this,
                new EconomyBalanceChangedEventArgs(
                    previousBalance,
                    Balance,
                    amount,
                    transactionId,
                    reason ?? string.Empty));
        }
    }
}
