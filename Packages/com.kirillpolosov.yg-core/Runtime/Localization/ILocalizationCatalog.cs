namespace YgCore.Localization
{
    /// <summary>
    /// Каталог строк игры: по языку и ключу выдаёт текст. Сами тексты хранит игра,
    /// а пакет (моки, заглушки, Яндекс-реализация) работает только через этот интерфейс.
    /// </summary>
    public interface ILocalizationCatalog
    {
        /// <summary>Язык по умолчанию. На него переключается всё неподдерживаемое.</summary>
        string DefaultLanguage { get; }

        /// <summary>
        /// Приводит код языка от платформы (например, «ru-RU» или «EN_us») к коду, который
        /// поддерживает каталог. Неизвестные и пустые значения превращаются в язык по умолчанию.
        /// </summary>
        string NormalizeLanguage(string language);

        /// <summary>
        /// Возвращает строку по ключу на указанном языке. Если ключа нет на этом языке,
        /// берётся язык по умолчанию, а если нет и там, возвращается сам ключ. Пустой ключ даёт пустую строку.
        /// Аргументы подставляются в строку как в <see cref="string.Format(string, object[])"/>.
        /// </summary>
        string Get(string language, string key, params object[] arguments);
    }
}
