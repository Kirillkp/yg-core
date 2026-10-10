using System;
using System.Collections.Generic;

namespace YgCore.Store
{
    /// <summary>
    /// Запись «в этой категории надет этот предмет» для данных прогресса. Игра включает список таких записей
    /// в свой класс данных. Имена полей (<c>category</c>, <c>itemId</c>) совпадают с «Обби Потоп»,
    /// поэтому его сохранения читаются без миграции.
    /// </summary>
    [Serializable]
    public sealed class EquippedItemProgressData
    {
        public int category;
        public string itemId;

        public EquippedItemProgressData()
        {
        }

        public EquippedItemProgressData(int category, string itemId)
        {
            this.category = category;
            this.itemId = itemId ?? string.Empty;
        }

        /// <summary>Превращает надетое в магазине в список для записи в прогресс (по возрастанию категории).</summary>
        public static List<EquippedItemProgressData> FromDictionary(IReadOnlyDictionary<int, string> equipped)
        {
            var result = new List<EquippedItemProgressData>();
            if (equipped != null)
            {
                foreach (var pair in equipped)
                {
                    result.Add(new EquippedItemProgressData(pair.Key, pair.Value));
                }
            }

            result.Sort((left, right) => left.category.CompareTo(right.category));
            return result;
        }

        /// <summary>
        /// Приводит список из прогресса к допустимому виду: без null, пустых идентификаторов и повторов категорий
        /// (при повторе остаётся первая запись), по возрастанию категории. Нужно в <c>Normalize</c> схемы игры.
        /// </summary>
        public static List<EquippedItemProgressData> Normalize(IEnumerable<EquippedItemProgressData> source)
        {
            var result = new List<EquippedItemProgressData>();
            var categories = new HashSet<int>();
            if (source != null)
            {
                foreach (var item in source)
                {
                    if (item != null
                        && !string.IsNullOrWhiteSpace(item.itemId)
                        && categories.Add(item.category))
                    {
                        result.Add(new EquippedItemProgressData(item.category, item.itemId));
                    }
                }
            }

            result.Sort((left, right) => left.category.CompareTo(right.category));
            return result;
        }

        /// <summary>Превращает список из прогресса в словарь «категория → предмет» для <c>RestoreProgress</c> магазина.</summary>
        public static Dictionary<int, string> ToDictionary(IEnumerable<EquippedItemProgressData> source)
        {
            var result = new Dictionary<int, string>();
            if (source != null)
            {
                foreach (var item in source)
                {
                    if (item != null)
                    {
                        result[item.category] = item.itemId;
                    }
                }
            }

            return result;
        }
    }
}
