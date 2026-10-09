using System.Threading.Tasks;

namespace YgCore.Contracts
{
    /// <summary>
    /// Хранилище сохранений «ключ → строка». Формат строки определяет слой выше (модуль Save).
    /// Ошибка загрузки не должна превращаться в «пустое» сохранение: отсутствие данных и сбой
    /// различаются статусом результата.
    /// </summary>
    public interface ISaveService
    {
        bool IsPlatformAvailable { get; }

        /// <summary>Загружает данные по ключу. Если данных по ключу нет, это успешный результат со значением null
        /// (не сбой). Сбой загрузки возвращается как Error, и вызывающий код не должен считать его пустым сохранением.</summary>
        Task<PlatformServiceResult<string>> LoadAsync(string key);

        /// <summary>Сохраняет данные. Success означает, что хранилище подтвердило запись.</summary>
        Task<PlatformServiceResult> SaveAsync(string key, string payload);

        /// <summary>Удаляет данные по ключу.</summary>
        Task<PlatformServiceResult> DeleteAsync(string key);
    }
}
