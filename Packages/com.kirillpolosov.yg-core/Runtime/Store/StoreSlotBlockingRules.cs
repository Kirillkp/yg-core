using System;
using System.Collections.Generic;

namespace YgCore.Store
{
    /// <summary>
    /// Готовые правила блокировки слотов по данным: для каждого предмета указано, какие категории он блокирует.
    /// Предмет не блокирует собственную категорию.
    /// </summary>
    public sealed class StoreSlotBlockingRules : IStoreEquipmentRules
    {
        private readonly Dictionary<string, int[]> blockedCategoriesByItemId =
            new Dictionary<string, int[]>();

        /// <param name="blockedCategoriesByItemId">Идентификатор товара → категории, которые он блокирует.</param>
        public StoreSlotBlockingRules(IReadOnlyDictionary<string, int[]> blockedCategoriesByItemId)
        {
            if (blockedCategoriesByItemId == null)
            {
                return;
            }

            foreach (var pair in blockedCategoriesByItemId)
            {
                if (!string.IsNullOrEmpty(pair.Key))
                {
                    this.blockedCategoriesByItemId[pair.Key] = pair.Value ?? new int[0];
                }
            }
        }

        public bool IsSlotBlocked(IReadOnlyDictionary<int, string> equippedItems, int category)
        {
            if (equippedItems == null)
            {
                return false;
            }

            foreach (var pair in equippedItems)
            {
                if (pair.Key == category
                    || string.IsNullOrEmpty(pair.Value)
                    || !blockedCategoriesByItemId.TryGetValue(pair.Value, out var blocked))
                {
                    continue;
                }

                if (Array.IndexOf(blocked, category) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public IReadOnlyDictionary<int, string> GetActiveEquipment(IReadOnlyDictionary<int, string> equippedItems)
        {
            var result = new Dictionary<int, string>();
            if (equippedItems == null)
            {
                return result;
            }

            foreach (var pair in equippedItems)
            {
                if (!string.IsNullOrEmpty(pair.Value) && !IsSlotBlocked(equippedItems, pair.Key))
                {
                    result[pair.Key] = pair.Value;
                }
            }

            return result;
        }
    }
}
