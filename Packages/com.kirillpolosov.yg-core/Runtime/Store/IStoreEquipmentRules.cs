using System.Collections.Generic;

namespace YgCore.Store
{
    /// <summary>
    /// Правила совместимости слотов экипировки. Например, полный костюм может блокировать слоты шапки и штанов:
    /// предметы заблокированных слотов остаются надетыми в сохранении, но не применяются к персонажу и не показываются активными.
    /// Правила задаёт игра; готовая реализация по данным: <see cref="StoreSlotBlockingRules"/>.
    /// </summary>
    public interface IStoreEquipmentRules
    {
        /// <summary>Заблокирован ли слот другим надетым предметом.</summary>
        /// <param name="equippedItems">Надетые предметы: категория → идентификатор товара.</param>
        /// <param name="category">Проверяемый слот.</param>
        bool IsSlotBlocked(IReadOnlyDictionary<int, string> equippedItems, int category);

        /// <summary>Надетые предметы, которые действительно применяются: предметы заблокированных слотов исключены.</summary>
        IReadOnlyDictionary<int, string> GetActiveEquipment(IReadOnlyDictionary<int, string> equippedItems);
    }
}
