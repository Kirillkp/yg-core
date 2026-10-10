using System;

namespace YgCore.WebQuality
{
    /// <summary>Правила выбора качества в виде чистых функций (без Unity), чтобы их можно было проверять без редактора.</summary>
    public static class WebQualityPolicy
    {
        /// <summary>Имя файла настроек в папке Resources.</summary>
        public const string ResourceName = "WebQualitySettings";

        /// <summary>
        /// Частота кадров для Unity: положительное значение остаётся как есть, ноль и отрицательные превращаются в -1,
        /// что означает «частоту выбирает браузер» (он лучше знает, какая подходит устройству).
        /// </summary>
        public static int NormalizeFrameRate(int requestedFrameRate)
        {
            return requestedFrameRate > 0 ? requestedFrameRate : -1;
        }

        /// <summary>
        /// Находит индекс уровня качества по имени для устройства. Возвращает -1, если такого уровня нет в сборке
        /// (его не создали или отключили для WebGL): тогда остаётся уровень по умолчанию.
        /// </summary>
        /// <param name="availableQualityNames">Имена уровней, включённых в сборку (<c>QualitySettings.names</c>).</param>
        /// <param name="isMobile">Мобильное ли устройство.</param>
        /// <param name="desktopQualityName">Имя уровня для компьютеров.</param>
        /// <param name="mobileQualityName">Имя уровня для телефонов.</param>
        public static int FindQualityIndex(
            string[] availableQualityNames,
            bool isMobile,
            string desktopQualityName,
            string mobileQualityName)
        {
            var wanted = isMobile ? mobileQualityName : desktopQualityName;
            return availableQualityNames == null || string.IsNullOrEmpty(wanted)
                ? -1
                : Array.IndexOf(availableQualityNames, wanted);
        }
    }
}
