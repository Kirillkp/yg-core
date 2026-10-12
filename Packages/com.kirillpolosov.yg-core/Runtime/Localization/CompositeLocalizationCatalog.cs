using System;
using System.Collections.Generic;
using System.Globalization;

namespace YgCore.Localization
{
    /// <summary>
    /// Каталог из нескольких каталогов. Нужен, когда строки лежат в разных местах: например, строки оболочки
    /// (шаблон игры) и строки самой игры. Игра собирает их в один каталог и отдаёт сервису локализации,
    /// который принимает только один <see cref="ILocalizationCatalog"/>.
    ///
    /// Правила:
    /// 1. Порядок каталогов это приоритет: первый опрашивается первым. Одинаковый ключ в двух каталогах
    ///    берётся из того, что стоит раньше (игра может переопределить строку оболочки: каталог игры ставится первым).
    /// 2. Сначала ищется строка на нужном языке во всех каталогах, и только потом на языке по умолчанию.
    ///    Так английский запасной вариант одного каталога не перебивает русскую строку другого.
    /// 3. У всех каталогов должен быть один язык по умолчанию, иначе конструктор бросает исключение.
    /// 4. Язык считается поддерживаемым, если его знает хотя бы один каталог.
    /// 5. Нет ключа ни в одном каталоге: возвращается сам ключ. Пустой ключ даёт пустую строку.
    ///
    /// Строгий поиск по языку работает для каталогов с <see cref="ILocalizationCatalogLookup"/>
    /// (его реализует <see cref="DictionaryLocalizationCatalog"/>). Для чужого каталога без него правило 2
    /// выполняется приближённо: его запасной вариант считается найденной строкой.
    /// </summary>
    public sealed class CompositeLocalizationCatalog : ILocalizationCatalog
    {
        private readonly ILocalizationCatalog[] catalogs;

        /// <param name="catalogs">Каталоги в порядке приоритета (первый главный). Нужен хотя бы один.</param>
        public CompositeLocalizationCatalog(params ILocalizationCatalog[] catalogs)
        {
            if (catalogs == null || catalogs.Length == 0)
            {
                throw new ArgumentException("Нужен хотя бы один каталог.", nameof(catalogs));
            }

            for (var index = 0; index < catalogs.Length; index++)
            {
                if (catalogs[index] == null)
                {
                    throw new ArgumentException("Каталог №" + index + " не задан.", nameof(catalogs));
                }

                if (catalogs[index].DefaultLanguage != catalogs[0].DefaultLanguage)
                {
                    throw new ArgumentException(
                        "У каталогов разные языки по умолчанию: «" + catalogs[0].DefaultLanguage
                        + "» и «" + catalogs[index].DefaultLanguage + "».",
                        nameof(catalogs));
                }
            }

            // Копия массива: изменение исходного списка игрой не должно менять порядок приоритета.
            this.catalogs = (ILocalizationCatalog[])catalogs.Clone();
            DefaultLanguage = catalogs[0].DefaultLanguage;
        }

        public string DefaultLanguage { get; }

        public string NormalizeLanguage(string language)
        {
            // Каталог, не знающий язык, отвечает языком по умолчанию. Значит, ответ, отличный от него,
            // означает «этот язык у меня есть». Первый такой ответ и берём.
            foreach (var catalog in catalogs)
            {
                var normalized = catalog.NormalizeLanguage(language);
                if (normalized != DefaultLanguage)
                {
                    return normalized;
                }
            }

            return DefaultLanguage;
        }

        public string Get(string language, string key, params object[] arguments)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            var normalizedLanguage = NormalizeLanguage(language);
            if (TryFind(normalizedLanguage, key, arguments, out var value))
            {
                return value;
            }

            if (normalizedLanguage != DefaultLanguage
                && TryFind(DefaultLanguage, key, arguments, out value))
            {
                return value;
            }

            // Ключа нет нигде: на экране виден пропуск, а игра не падает.
            return key;
        }

        private bool TryFind(string language, string key, object[] arguments, out string value)
        {
            foreach (var catalog in catalogs)
            {
                if (catalog is ILocalizationCatalogLookup lookup)
                {
                    if (lookup.TryGetTemplate(language, key, out var template))
                    {
                        value = arguments == null || arguments.Length == 0
                            ? template
                            : string.Format(CultureInfo.InvariantCulture, template, arguments);
                        return true;
                    }

                    continue;
                }

                // Каталог без строгого поиска: «ключ вернулся как есть» значит «строки нет».
                var result = catalog.Get(language, key, arguments);
                if (result != key)
                {
                    value = result;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
