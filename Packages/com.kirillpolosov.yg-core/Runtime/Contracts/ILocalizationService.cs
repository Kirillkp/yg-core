using System;
using System.Threading.Tasks;

namespace YgCore.Contracts
{
    /// <summary>Локализация: определение языка платформы и выдача строк на выбранном языке.</summary>
    public interface ILocalizationService
    {
        event EventHandler<LanguageChangedEventArgs> LanguageChanged;

        bool IsPlatformAvailable { get; }

        /// <summary>Текущий язык (код, например «ru» или «en»).</summary>
        string CurrentLanguage { get; }

        /// <summary>Определяет язык по настройкам платформы и игрока.</summary>
        Task<PlatformServiceResult<string>> DetectLanguageAsync();

        /// <summary>Возвращает строку по ключу на текущем языке. Аргументы подставляются в формат строки.</summary>
        string Get(string key, params object[] arguments);

        /// <summary>Переключает язык.</summary>
        void SetLanguage(string language);
    }
}
