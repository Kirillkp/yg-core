using System;
using System.Collections.Generic;
using System.Globalization;

namespace YgCore.Localization
{
    /// <summary>
    /// Каталог строк на словарях: игра передаёт таблицы «язык → (ключ → текст)».
    /// Поведение совпадает с каталогом «Обби Потоп»: при двух языках (ru и en) и языке по умолчанию en
    /// результат тот же, что был у прежнего статического каталога.
    /// </summary>
    public sealed class DictionaryLocalizationCatalog : ILocalizationCatalog
    {
        private static readonly char[] LanguageSeparators = { '-', '_' };

        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tables;

        /// <param name="defaultLanguage">Код языка по умолчанию, например «en». Для него обязательна таблица.</param>
        /// <param name="tables">Таблицы строк по кодам языков (коды в нижнем регистре).</param>
        public DictionaryLocalizationCatalog(
            string defaultLanguage,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tables)
        {
            if (string.IsNullOrWhiteSpace(defaultLanguage))
            {
                throw new ArgumentException("Язык по умолчанию не задан.", nameof(defaultLanguage));
            }

            this.tables = tables ?? throw new ArgumentNullException(nameof(tables));
            DefaultLanguage = defaultLanguage.Trim().ToLowerInvariant();

            if (!this.tables.ContainsKey(DefaultLanguage))
            {
                throw new ArgumentException(
                    "Для языка по умолчанию «" + DefaultLanguage + "» нет таблицы строк.",
                    nameof(tables));
            }
        }

        public string DefaultLanguage { get; }

        public string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return DefaultLanguage;
            }

            // Берём только основную часть кода: «ru-RU» и «ru_RU» превращаются в «ru».
            var normalized = language.Trim().ToLowerInvariant();
            var separatorIndex = normalized.IndexOfAny(LanguageSeparators);
            if (separatorIndex > 0)
            {
                normalized = normalized.Substring(0, separatorIndex);
            }

            return tables.ContainsKey(normalized) ? normalized : DefaultLanguage;
        }

        public string Get(string language, string key, params object[] arguments)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            // Сначала таблица нужного языка, затем язык по умолчанию. Если ключа нет нигде,
            // возвращаем сам ключ: на экране виден пропуск, а игра не падает.
            var table = tables[NormalizeLanguage(language)];
            if (!table.TryGetValue(key, out var value)
                && !tables[DefaultLanguage].TryGetValue(key, out value))
            {
                return key;
            }

            return arguments == null || arguments.Length == 0
                ? value
                : string.Format(CultureInfo.InvariantCulture, value, arguments);
        }
    }
}
