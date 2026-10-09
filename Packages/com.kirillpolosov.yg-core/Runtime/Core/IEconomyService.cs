using System;

namespace YgCore.Core
{
    /// <summary>
    /// Игровая валюта. Баланс меняется только транзакциями с уникальным идентификатором:
    /// повторная обработка той же транзакции игнорируется, поэтому двойное начисление
    /// (например, из-за повторного вызова после сбоя сети) невозможно.
    /// Интерфейс экрана не должен менять баланс иначе, чем через этот сервис.
    /// </summary>
    public interface IEconomyService
    {
        /// <summary>Срабатывает после каждого изменения баланса.</summary>
        event EventHandler<EconomyBalanceChangedEventArgs> BalanceChanged;

        /// <summary>Текущий баланс, не бывает отрицательным.</summary>
        int Balance { get; }

        /// <summary>Начислить сумму. Возвращает false, если сумма не больше нуля,
        /// идентификатор пустой или такая транзакция уже обработана.</summary>
        bool TryCredit(string transactionId, int amount, string reason);

        /// <summary>Списать сумму. Дополнительно возвращает false, если денег не хватает.</summary>
        bool TrySpend(string transactionId, int amount, string reason);

        /// <summary>Обрабатывалась ли транзакция с таким идентификатором.</summary>
        bool HasProcessed(string transactionId);
    }
}
