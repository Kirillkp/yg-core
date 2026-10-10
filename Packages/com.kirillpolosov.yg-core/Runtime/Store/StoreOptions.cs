using System.Collections.Generic;

namespace YgCore.Store
{
    /// <summary>Настройки магазина, которые зависят от устройства игры.</summary>
    public sealed class StoreOptions
    {
        /// <summary>
        /// Предметы, которые есть у каждого игрока с самого начала и надеты по умолчанию: категория → идентификатор товара.
        /// Например, базовый скин персонажа. Такая категория всегда занята: базовый предмет возвращается, если надетого нет.
        /// </summary>
        public Dictionary<int, string> DefaultItems = new Dictionary<int, string>();

        /// <summary>
        /// Категории, предметы которых можно снять (аксессуары). Повторное «надеть» уже надетого предмета такой категории снимает его.
        /// В остальных категориях предмет остаётся надетым.
        /// </summary>
        public HashSet<int> OptionalCategories = new HashSet<int>();
    }
}
