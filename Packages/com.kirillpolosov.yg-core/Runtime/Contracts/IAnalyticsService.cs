using System.Collections.Generic;
using System.Threading.Tasks;

namespace YgCore.Contracts
{
    /// <summary>
    /// Отправка событий аналитики. Аналитика никогда не влияет на игровую логику:
    /// ошибка отправки не должна ни на что влиять.
    /// </summary>
    public interface IAnalyticsService
    {
        bool IsPlatformAvailable { get; }

        /// <summary>Отправляет событие. Параметры необязательны.</summary>
        Task<PlatformServiceResult> TrackEventAsync(string eventName, Dictionary<string, string> parameters = null);
    }
}
