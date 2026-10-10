using System;
using System.Collections.Generic;

namespace YgCore.Save
{
    /// <summary>Помощники для схем игры: приведение списков идентификаторов (покупки, предметы, коллекции).</summary>
    public static class ProgressionLists
    {
        /// <summary>
        /// Список без null, пустых строк и повторов, отсортированный по порядковому сравнению.
        /// Сортировка нужна, чтобы одинаковые данные всегда сериализовались одинаково.
        /// </summary>
        public static List<string> Distinct(IEnumerable<string> source)
        {
            var result = new List<string>();
            var known = new HashSet<string>();
            if (source != null)
            {
                foreach (var value in source)
                {
                    if (!string.IsNullOrWhiteSpace(value) && known.Add(value))
                    {
                        result.Add(value);
                    }
                }
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>Объединение двух списков без повторов, отсортированное. Подходит для слияния гостя с облаком.</summary>
        public static List<string> Union(IEnumerable<string> first, IEnumerable<string> second)
        {
            var result = Distinct(first);
            var known = new HashSet<string>(result);
            if (second != null)
            {
                foreach (var value in second)
                {
                    if (!string.IsNullOrWhiteSpace(value) && known.Add(value))
                    {
                        result.Add(value);
                    }
                }
            }

            result.Sort(StringComparer.Ordinal);
            return result;
        }
    }
}
